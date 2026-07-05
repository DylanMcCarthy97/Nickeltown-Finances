using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

public sealed class SupplierDetectionService : ISupplierDetectionService
{
    private readonly ISupplierProfileRepository _profiles;
    private readonly ICategoryRepository _categories;

    private static readonly (string Name, string[] Aliases, string DefaultCategoryKeyword)[] KnownSuppliers =
    [
        ("Bunnings", ["BUNNINGS WAREHOUSE", "BUNNINGS"], "Maintenance"),
        ("Officeworks", ["OFFICEWORKS"], "Office"),
        ("Liquor Barons", ["LIQUOR BARONS", "LIQUORBARONS"], "Alcohol"),
        ("Woolworths", ["WOOLWORTHS", "WOOLWORTHS METRO"], "Groceries"),
        ("Coles", ["COLES", "COLES EXPRESS"], "Groceries"),
        ("ALDI", ["ALDI STORES"], "Groceries"),
        ("Mitre 10", ["MITRE 10", "MITRE10"], "Maintenance"),
        ("Repco", ["REPCO AUTO"], "Maintenance"),
        ("Auto One", ["AUTO ONE"], "Maintenance"),
        ("Jaycar", ["JAYCAR ELECTRONICS"], "Maintenance"),
        ("Harvey Norman", ["HARVEY NORMAN"], "Equipment"),
        ("Beacon", ["BEACON LIGHTING"], "Maintenance"),
        ("Shell", ["SHELL COLES EXPRESS", "SHELL REDDY"], "Fuel"),
        ("BP", ["BP CONNECT", "BP EXPRESS"], "Fuel"),
        ("Ampol", ["AMPOL", "CALTEX"], "Fuel"),
        ("Caltex", ["CALTEX STARSHOP"], "Fuel"),
        ("United", ["UNITED PETROLEUM"], "Fuel"),
        ("Costco", ["COSTCO WHOLESALE"], "Groceries")
    ];

    public SupplierDetectionService(
        ISupplierProfileRepository profiles,
        ICategoryRepository categories)
    {
        _profiles = profiles;
        _categories = categories;
    }

    public Task<SupplierDetectionResult?> DetectAsync(
        ReceiptImportItem item,
        ReceiptOcrInfo ocr,
        CancellationToken cancellationToken = default)
    {
        var searchText = BuildSearchText(item, ocr);
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult<SupplierDetectionResult?>(null);

        SupplierProfile? profile = null;
        byte confidence = 0;

        if (!string.IsNullOrWhiteSpace(ocr.EffectiveAbn))
        {
            profile = _profiles.FindByAbn(ocr.EffectiveAbn!);
            if (profile is not null)
                confidence = Math.Max(confidence, (byte)95);
        }

        if (profile is null)
        {
            foreach (var known in KnownSuppliers)
            {
                if (!ContainsAny(searchText, known.Name, known.Aliases))
                    continue;

                profile = EnsureProfile(known.Name, known.Aliases, known.DefaultCategoryKeyword);
                confidence = Math.Max(confidence, (byte)88);
                break;
            }
        }

        if (profile is null && !string.IsNullOrWhiteSpace(ocr.EffectiveSupplier))
        {
            profile = _profiles.FindByName(ocr.EffectiveSupplier!)
                ?? EnsureProfile(ocr.EffectiveSupplier!, [], null);
            confidence = Math.Max(confidence, (byte)72);
        }

        if (profile is null)
            return Task.FromResult<SupplierDetectionResult?>(null);

        profile.Confidence = Math.Max(profile.Confidence, confidence);
        _profiles.Update(profile);

        return Task.FromResult<SupplierDetectionResult?>(new SupplierDetectionResult
        {
            ProfileId = profile.Id,
            SupplierName = profile.Name,
            Confidence = confidence,
            SuggestedCategoryId = profile.DefaultCategoryId,
            SuggestedCategoryName = profile.DefaultCategoryName,
            DefaultPaymentMethod = profile.DefaultPaymentMethod,
            DefaultGstRegistered = profile.DefaultGstRegistered
        });
    }

    public Task RecordPurchaseAsync(
        ObjectId profileId,
        decimal amount,
        DateTime purchaseDate,
        ObjectId? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var profile = _profiles.GetById(profileId);
        if (profile is null) return Task.CompletedTask;

        profile.PurchaseCount++;
        profile.TotalSpent += amount;
        profile.LastPurchaseDate = purchaseDate;
        profile.LargestSpend = Math.Max(profile.LargestSpend, amount);
        profile.AverageSpend = profile.PurchaseCount == 0 ? 0 : profile.TotalSpent / profile.PurchaseCount;
        profile.Confidence = (byte)Math.Min(100, profile.Confidence + 2);

        if (categoryId is { } catId && catId != ObjectId.Empty)
        {
            profile.DefaultCategoryId = catId;
            var cat = _categories.GetById(catId);
            profile.DefaultCategoryName = cat?.Name;
        }

        _profiles.Update(profile);
        return Task.CompletedTask;
    }

    private SupplierProfile EnsureProfile(string name, IEnumerable<string> aliases, string? categoryKeyword)
    {
        var existing = _profiles.FindByName(name);
        if (existing is not null)
            return existing;

        ObjectId? categoryId = null;
        string? categoryName = null;
        if (!string.IsNullOrWhiteSpace(categoryKeyword))
        {
            var cat = _categories.GetAll()
                .FirstOrDefault(c => c.Name.Contains(categoryKeyword, StringComparison.OrdinalIgnoreCase));
            categoryId = cat?.Id;
            categoryName = cat?.Name;
        }

        var profile = new SupplierProfile
        {
            Name = name,
            Aliases = aliases.ToList(),
            DefaultCategoryId = categoryId,
            DefaultCategoryName = categoryName,
            Confidence = 70
        };
        _profiles.Insert(profile);
        return profile;
    }

    private static string BuildSearchText(ReceiptImportItem item, ReceiptOcrInfo ocr) =>
        string.Join('\n', new[]
        {
            ocr.EffectiveSupplier,
            ocr.EffectiveAbn,
            ocr.FullText,
            item.FileName
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

    private static bool ContainsAny(string haystack, string name, IEnumerable<string> aliases)
    {
        if (haystack.Contains(name, StringComparison.OrdinalIgnoreCase))
            return true;
        return aliases.Any(a => haystack.Contains(a, StringComparison.OrdinalIgnoreCase));
    }
}
