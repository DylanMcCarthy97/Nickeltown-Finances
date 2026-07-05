using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

/// <summary>
/// Rule-based category suggestions from OCR supplier text and supplier purchase history.
/// </summary>
public sealed class RuleBasedReceiptAiParser : IReceiptAiParser
{
    private readonly ICategorisationService _categorisationService;
    private readonly ISupplierProfileRepository _supplierProfiles;

    private static readonly (string Keyword, string CategoryHint)[] SupplierCategoryHints =
    [
        ("BUNNINGS", "Maintenance"),
        ("OFFICEWORKS", "Office"),
        ("LIQUOR", "Alcohol"),
        ("WOOLWORTHS", "Groceries"),
        ("COLES", "Groceries"),
        ("ALDI", "Groceries"),
        ("MITRE", "Maintenance"),
        ("REPCO", "Maintenance"),
        ("JAYCAR", "Maintenance"),
        ("SHELL", "Fuel"),
        ("BP ", "Fuel"),
        ("AMPOL", "Fuel"),
        ("CALTEX", "Fuel"),
        ("COSTCO", "Groceries")
    ];

    public RuleBasedReceiptAiParser(
        ICategorisationService categorisationService,
        ISupplierProfileRepository supplierProfiles)
    {
        _categorisationService = categorisationService;
        _supplierProfiles = supplierProfiles;
    }

    public bool IsAvailable => true;

    public async Task<ReceiptAiSuggestionInfo?> ParseAsync(
        ReceiptImportItem item,
        ReceiptOcrInfo ocr,
        CancellationToken cancellationToken = default)
    {
        if (item.DetectedSupplierProfileId is { } profileId && profileId != ObjectId.Empty)
        {
            var profile = _supplierProfiles.GetById(profileId);
            if (profile?.DefaultCategoryId is { } catId && catId != ObjectId.Empty)
            {
                var historyBoost = Math.Min(15, profile.PurchaseCount / 2);
                var confidence = (byte)Math.Min(100, 75 + historyBoost + profile.Confidence / 10);
                return new ReceiptAiSuggestionInfo
                {
                    CategoryId = catId,
                    CategoryName = profile.DefaultCategoryName,
                    Confidence = confidence
                };
            }
        }

        var searchText = string.Join(' ', new[]
        {
            ocr.EffectiveSupplier,
            item.DetectedSupplierName,
            item.FileName,
            ocr.EffectivePaymentMethod,
            ocr.FullText
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        if (string.IsNullOrWhiteSpace(searchText))
            return null;

        foreach (var hint in SupplierCategoryHints)
        {
            if (!searchText.Contains(hint.Keyword, StringComparison.OrdinalIgnoreCase))
                continue;

            var (categoryId, categoryName) = await _categorisationService.SuggestAsync(hint.CategoryHint, isIncome: false);
            if (categoryId is null || categoryId == ObjectId.Empty)
                continue;

            return new ReceiptAiSuggestionInfo
            {
                CategoryId = categoryId,
                CategoryName = categoryName,
                Confidence = 82
            };
        }

        var (suggestedId, suggestedName) = await _categorisationService.SuggestAsync(searchText, isIncome: false);
        if (suggestedId is null || suggestedId == ObjectId.Empty)
            return null;

        return new ReceiptAiSuggestionInfo
        {
            CategoryId = suggestedId,
            CategoryName = suggestedName,
            Confidence = 70
        };
    }
}
