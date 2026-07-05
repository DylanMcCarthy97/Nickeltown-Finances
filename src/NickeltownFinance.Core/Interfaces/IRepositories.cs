using LiteDB;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Core.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    T? GetById(ObjectId id);

    IEnumerable<T> GetAll();

    void Insert(T entity);

    bool Update(T entity);

    bool Delete(ObjectId id);
}

public interface IUserRepository : IRepository<User>
{
    IEnumerable<User> GetActiveUsers();

    User? GetByUsername(string username);

    User? GetByRole(Enums.UserRole role);

    int CountAdministrators(ObjectId? excludeUserId = null);
}

public interface IAuditLogRepository
{
    void Insert(AuditLogEntry entry);

    IEnumerable<AuditLogEntry> GetRecent(int limit = 100);
}

public interface ICategoryRepository : IRepository<Category>
{
    IEnumerable<Category> GetByType(Enums.CategoryType type);

    IEnumerable<Category> GetActive();

    int GetUsageCount(ObjectId categoryId);
}

public interface IFinancialYearRepository : IRepository<FinancialYear>
{
    FinancialYear? GetCurrent();

    FinancialYear? GetByDate(DateTime date);

    FinancialYear? GetByName(string name);

    bool Any();
}

public interface ITransactionRepository : IRepository<Transaction>
{
    IEnumerable<Transaction> GetByFinancialYear(ObjectId financialYearId, bool includeDeleted = false);

    IEnumerable<Transaction> GetByImportBatch(ObjectId batchId, bool includeDeleted = false);

    IEnumerable<Transaction> GetDeleted();

    IEnumerable<Transaction> GetByDateRange(DateTime start, DateTime end, ObjectId? financialYearFilter = null);

    IEnumerable<Transaction> FindByFingerprint(string fingerprint);

    IEnumerable<Transaction> FindPotentialDuplicates(DateTime date, decimal amount, string description, string reference);

    void InsertMany(IEnumerable<Transaction> transactions);
}

public interface IAttachmentRepository : IRepository<Attachment>
{
    IEnumerable<Attachment> GetByTransaction(ObjectId transactionId);

    IEnumerable<Attachment> GetByTransactions(IEnumerable<ObjectId> transactionIds);

    IEnumerable<Attachment> SearchByFileName(string search);

    IEnumerable<Attachment> SearchByOcrText(string search);

    HashSet<ObjectId> GetTransactionIdsWithAttachments(AttachmentKind? kind = null);
}

public interface ICategorisationRuleRepository : IRepository<CategorisationRule>
{
    CategorisationRule? FindBestMatch(string description);

    IEnumerable<CategorisationRule> GetAllOrdered();

    CategorisationRule? FindByMatchText(string matchText);
}

public interface IImportBatchRepository : IRepository<ImportBatch>
{
    IEnumerable<ImportBatch> GetHistory(int limit = 100);

    ImportBatch? GetLatestBySource(ImportSourceType sourceType);
}

public interface ISquareDepositRepository : IRepository<SquareDeposit>
{
    SquareDeposit? GetByExternalDepositId(string depositId);

    IEnumerable<SquareDeposit> GetActive();

    IEnumerable<SquareDeposit> GetByStatus(SquareDepositStatus status);

    IEnumerable<SquareDeposit> GetUnmatched();

    IEnumerable<SquareDeposit> GetByImportBatch(ObjectId batchId, bool includeDeleted = false);

    IEnumerable<SquareDeposit> FindByNetAmount(decimal netAmount);

    void InsertMany(IEnumerable<SquareDeposit> deposits);
}

public interface ISquareTransactionRepository : IRepository<SquareTransaction>
{
    IEnumerable<SquareTransaction> GetByDeposit(ObjectId depositId);

    IEnumerable<SquareTransaction> GetByImportBatch(ObjectId batchId, bool includeDeleted = false);

    IEnumerable<SquareTransaction> FindByFingerprint(string fingerprint);

    void InsertMany(IEnumerable<SquareTransaction> transactions);
}

public interface ISettingsRepository
{
    string? GetValue(string key);

    void SetValue(string key, string value);

    T? GetValue<T>(string key);

    void SetValue<T>(string key, T value);
}

public interface IReceiptImportItemRepository : IRepository<ReceiptImportItem>
{
    IEnumerable<ReceiptImportItem> GetInbox(bool includeCommitted = false);

    IEnumerable<ReceiptImportItem> GetAllExceptIgnored();

    IEnumerable<ReceiptImportItem> GetByUploadSessionKey(string uploadSessionKey);

    IEnumerable<ReceiptImportItem> GetByStatus(ReceiptImportStatus status);

    ReceiptImportItem? FindByFileHash(string fileHash);

    IEnumerable<ReceiptImportItem> Search(string query, bool includeCommitted = false);
}

public interface IReceiptImportBatchRepository : IRepository<ReceiptImportBatch>
{
    IEnumerable<ReceiptImportBatch> GetHistory(int limit = 100);
}

