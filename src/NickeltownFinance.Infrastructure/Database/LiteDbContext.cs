using LiteDB;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Database;

public sealed class LiteDbContext : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly object _sync = new();
    private bool _disposed;

    public LiteDbContext()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.DatabasePath)!);
        _database = new LiteDatabase(AppPaths.DatabasePath);
        MigrateFinancialYears();
        EnsureIndexes();
    }

    public ILiteCollection<Transaction> Transactions => _database.GetCollection<Transaction>("transactions");

    public ILiteCollection<Category> Categories => _database.GetCollection<Category>("categories");

    public ILiteCollection<FinancialYear> FinancialYears => _database.GetCollection<FinancialYear>("financial_years");

    public ILiteCollection<Core.Models.User> Users => _database.GetCollection<Core.Models.User>("users");


    public ILiteCollection<AppSetting> Settings => _database.GetCollection<AppSetting>("settings");

    public ILiteCollection<Attachment> Attachments => _database.GetCollection<Attachment>("attachments");

    public ILiteCollection<CategorisationRule> CategorisationRules => _database.GetCollection<CategorisationRule>("categorisation_rules");

    public ILiteCollection<ImportBatch> ImportBatches => _database.GetCollection<ImportBatch>("import_batches");

    public ILiteCollection<SquareDeposit> SquareDeposits => _database.GetCollection<SquareDeposit>("square_deposits");

    public ILiteCollection<SquareTransaction> SquareTransactions => _database.GetCollection<SquareTransaction>("square_transactions");

    public ILiteCollection<AuditLogEntry> AuditLogs => _database.GetCollection<AuditLogEntry>("audit_logs");

    public ILiteCollection<ReceiptImportItem> ReceiptImportItems =>
        _database.GetCollection<ReceiptImportItem>("receipt_import_items");

    public ILiteCollection<ReceiptImportBatch> ReceiptImportBatches =>
        _database.GetCollection<ReceiptImportBatch>("receipt_import_batches");

    public ILiteCollection<SupplierProfile> SupplierProfiles =>
        _database.GetCollection<SupplierProfile>("supplier_profiles");

    public ILiteCollection<SupplierPurchase> SupplierPurchases =>
        _database.GetCollection<SupplierPurchase>("supplier_purchases");

    public ILiteCollection<SupplierProduct> SupplierProducts =>
        _database.GetCollection<SupplierProduct>("supplier_products");

    private void EnsureIndexes()
    {
        Transactions.EnsureIndex(x => x.Date);
        Transactions.EnsureIndex(x => x.FinancialYearId);
        Transactions.EnsureIndex(x => x.CategoryId);
        Transactions.EnsureIndex(x => x.IsDeleted);
        Transactions.EnsureIndex(x => x.ImportFingerprint);
        Transactions.EnsureIndex(x => x.Reference);
        Transactions.EnsureIndex(x => x.SquareDepositId);
        Transactions.EnsureIndex(x => x.IsSquareDeposit);
        Categories.EnsureIndex(x => x.Type);
        FinancialYears.EnsureIndex(x => x.IsActive);
        FinancialYears.EnsureIndex(x => x.Name);
        Settings.EnsureIndex(x => x.Key);
        Attachments.EnsureIndex(x => x.TransactionId);
        Attachments.EnsureIndex(x => x.FileName);
        Attachments.EnsureIndex(x => x.Kind);
        CategorisationRules.EnsureIndex(x => x.MatchText);
        ImportBatches.EnsureIndex(x => x.ImportedAt);
        ImportBatches.EnsureIndex(x => x.SourceType);
        SquareDeposits.EnsureIndex(x => x.DepositId);
        SquareDeposits.EnsureIndex(x => x.DepositDate);
        SquareDeposits.EnsureIndex(x => x.NetAmount);
        SquareDeposits.EnsureIndex(x => x.Status);
        SquareDeposits.EnsureIndex(x => x.ImportFingerprint);
        SquareTransactions.EnsureIndex(x => x.DepositId);
        SquareTransactions.EnsureIndex(x => x.ExternalDepositId);
        SquareTransactions.EnsureIndex(x => x.ImportFingerprint);
        AuditLogs.EnsureIndex(x => x.TimestampUtc);
        AuditLogs.EnsureIndex(x => x.Action);
        ReceiptImportItems.EnsureIndex(x => x.Status);
        ReceiptImportItems.EnsureIndex(x => x.CreatedDate);
        ReceiptImportItems.EnsureIndex(x => x.FileHash);
        ReceiptImportItems.EnsureIndex(x => x.SuggestedMatchTransactionId);
        ReceiptImportBatches.EnsureIndex(x => x.ImportedAt);
        SupplierProfiles.EnsureIndex(x => x.Name);
        SupplierPurchases.EnsureIndex(x => x.SupplierProfileId);
        SupplierPurchases.EnsureIndex(x => x.PurchaseDate);
        SupplierPurchases.EnsureIndex(x => x.ReceiptImportItemId);
        SupplierProducts.EnsureIndex(x => x.SupplierProfileId);
        SupplierProducts.EnsureIndex(x => x.NormalizedKey);

        // Legacy PIN-era users have no Username; unique index cannot be created over duplicate nulls.
        foreach (var user in Users.FindAll().Where(u => string.IsNullOrWhiteSpace(u.Username)).ToList())
            Users.Delete(user.Id);

        Users.EnsureIndex(nameof(Core.Models.User.Username), unique: true);
    }



    /// <summary>
    /// Renames legacy fields (StartDate → OpeningDate, IsCurrent → IsActive)
    /// and adds Notes / IsArchived for existing databases.
    /// </summary>
    private void MigrateFinancialYears()
    {
        var col = _database.GetCollection("financial_years");
        foreach (var doc in col.FindAll().ToList())
        {
            var changed = false;

            if (doc.ContainsKey("StartDate") && !doc.ContainsKey("OpeningDate"))
            {
                doc["OpeningDate"] = doc["StartDate"];
                doc.Remove("StartDate");
                changed = true;
            }

            if (doc.ContainsKey("IsCurrent") && !doc.ContainsKey("IsActive"))
            {
                doc["IsActive"] = doc["IsCurrent"];
                doc.Remove("IsCurrent");
                changed = true;
            }

            if (!doc.ContainsKey("Notes"))
            {
                doc["Notes"] = string.Empty;
                changed = true;
            }

            if (!doc.ContainsKey("IsArchived"))
            {
                doc["IsArchived"] = false;
                changed = true;
            }

            if (!doc.ContainsKey("IsLocked"))
            {
                doc["IsLocked"] = false;
                changed = true;
            }

            // V2: StartingDate / StartingBalance — user-entered bank balance at start of tracking.
            // OpeningBalance remains the auto-calculated ledger opening.
            var openingDate = doc.ContainsKey("OpeningDate") ? doc["OpeningDate"].AsDateTime : default;
            var openingBalance = doc.ContainsKey("OpeningBalance") ? doc["OpeningBalance"].AsDecimal : 0m;

            if (!doc.ContainsKey("StartingDate") || doc["StartingDate"].AsDateTime == default)
            {
                doc["StartingDate"] = openingDate;
                changed = true;
            }

            if (!doc.ContainsKey("StartingBalance"))
            {
                doc["StartingBalance"] = openingBalance;
                changed = true;
            }

            if (changed)
                col.Update(doc);
        }

        MigrateCategories();
    }

    private void MigrateCategories()
    {
        var col = _database.GetCollection("categories");
        foreach (var doc in col.FindAll().ToList())
        {
            var changed = false;

            if (!doc.ContainsKey("Icon"))
            {
                doc["Icon"] = "Tag24";
                changed = true;
            }

            if (!doc.ContainsKey("IsArchived"))
            {
                doc["IsArchived"] = false;
                changed = true;
            }

            if (changed)
                col.Update(doc);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        lock (_sync)
            _database.Dispose();
    }

    internal T ExecuteLocked<T>(Func<T> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_sync)
            return action();
    }

    internal void ExecuteLocked(Action action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_sync)
            action();
    }

    /// <summary>Flush pending writes so the database file can be copied while the app is running.</summary>
    public void Checkpoint()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_sync)
            _database.Checkpoint();
    }
}

