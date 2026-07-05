using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Core.Interfaces;

public interface ISessionService
{
    User? CurrentUser { get; }

    bool IsLoggedIn { get; }

    void SetUser(User user);

    void Clear();
}

public interface IAuthenticationService
{
    Task<(bool Success, string? Error)> LoginAsync(string username, string password, bool rememberUsername);

    Task<(bool Success, string? Error)> ChangePasswordAsync(ObjectId userId, string currentPassword, string newPassword);

    Task<(bool Success, string? Error)> AdminResetPasswordAsync(string adminUsername, string adminPassword, string targetUsername, string newPassword);

    Task<(bool Success, string? Error)> ResetUserPasswordAsync(ObjectId targetUserId, string adminPassword, string newPassword);

    bool VerifyCurrentUserPassword(string password);
}

public interface IDataSeederService
{
    void SeedIfNeeded();

    bool IsSetupRequired();

    Task<(bool Success, string? Error)> CompleteFirstRunSetupAsync(FirstRunSetupRequest request);
}

public interface ISettingsService
{
    string ClubName { get; set; }

    string? ClubLogoPath { get; set; }

    /// <summary>Path to the treasurer digital signature image used on PDF reports.</summary>
    string? TreasurerSignaturePath { get; set; }

    int FinancialYearStartMonth { get; set; }

    /// <summary>Date the club began tracking in the app (bank balance is as of this date).</summary>
    DateTime? TrackingStartDate { get; set; }

    /// <summary>Bank balance on <see cref="TrackingStartDate"/>.</summary>
    decimal? TrackingStartBalance { get; set; }

    AppTheme Theme { get; set; }

    string BackupFolder { get; set; }

    string LastUsername { get; set; }

    bool RememberUsername { get; set; }

    double? WindowLeft { get; set; }

    double? WindowTop { get; set; }

    double? WindowWidth { get; set; }

    double? WindowHeight { get; set; }

    string? WindowState { get; set; }

    bool SidebarCollapsed { get; set; }

    void Save();
}


public interface ITransactionService
{
    Task<IReadOnlyList<TransactionListItem>> GetLedgerAsync(
        ObjectId financialYearId,
        string? search = null,
        ObjectId? categoryFilter = null,
        bool includeDeleted = false,
        TransactionSearchFilter? filter = null);

    Task<TransactionListItem?> GetByIdAsync(ObjectId id);

    Task SaveAsync(Transaction transaction, bool isIncome);

    Task SoftDeleteAsync(ObjectId id);

    Task RestoreAsync(ObjectId id);

    Task DuplicateAsync(ObjectId id);

    Task<string> ExportLedgerExcelAsync(ObjectId financialYearId, string outputPath, string? search = null, ObjectId? categoryFilter = null);

    decimal CalculateRunningBalance(ObjectId financialYearId, DateTime asOfDate, ObjectId? upToTransactionId = null);
}

public interface IStatementImportService
{
    /// <summary>Analyse an ANZ statement. On format failure, Failure is set (never a vague error).</summary>
    Task<ImportAnalyseResult> AnalyseAsync(string filePath, AnzColumnMapping? mapping = null);

    Task<ImportResult> CommitAsync(ImportCommitRequest request);

    /// <summary>Soft-deletes all transactions from an import batch and recalculates opening balances.</summary>
    Task<(bool Success, string? Error)> UndoImportAsync(ObjectId batchId);

    Task<IReadOnlyList<ImportHistoryItem>> GetHistoryAsync();

    Task<ImportStatusSummary> GetImportStatusAsync();
}

public interface ISquareImportService
{
    Task<SquareImportAnalyseResult> AnalyseAsync(string filePath);

    Task<ImportResult> CommitAsync(SquareImportCommitRequest request);

    Task<(bool Success, string? Error)> UndoImportAsync(ObjectId batchId);
}

public interface IReconciliationService
{
    Task<ReconciliationSummary> GetSummaryAsync();

    /// <summary>Auto-match Square deposit transfers among the given bank transactions.</summary>
    Task<(int Matched, int NeedsReview)> MatchTransactionsAsync(IReadOnlyList<ObjectId> transactionIds);

    Task<(bool Success, string? Error)> ManualMatchAsync(ObjectId transactionId, ObjectId squareDepositId);

    Task<(bool Success, string? Error)> UnmatchAsync(ObjectId transactionId);
}

public interface ICategorisationService
{
    Task<CategorisationSuggestion> SuggestDetailedAsync(string description, bool isIncome);

    Task<(ObjectId? CategoryId, string CategoryName)> SuggestAsync(string description, bool isIncome);

    Task RememberAsync(string description, ObjectId categoryId);

    Task<IReadOnlyList<CategorisationRuleItem>> GetRulesAsync();

    Task SaveRuleAsync(CategorisationRule rule);

    Task DeleteRuleAsync(ObjectId id);

    /// <summary>Seeds default merchant rules when missing (safe to call repeatedly).</summary>
    void EnsureDefaultRules();
}

public interface IAttachmentService
{
    Task<IReadOnlyList<AttachmentInfo>> GetForTransactionAsync(ObjectId transactionId);

    Task<AttachmentInfo> AddAsync(ObjectId transactionId, string sourceFilePath, AttachmentKind kind);

    Task<AttachmentInfo> AddFromBytesAsync(ObjectId transactionId, byte[] data, string fileName, AttachmentKind kind);

    /// <summary>Commits an inbox receipt to a transaction, copying OCR metadata from the import item.</summary>
    Task<AttachmentInfo> AddFromInboxAsync(
        ObjectId transactionId,
        string sourceFilePath,
        string fileName,
        AttachmentKind kind,
        ReceiptImportItem importItem);

    Task DeleteAsync(ObjectId attachmentId);

    Task<string> GetFullPathAsync(ObjectId attachmentId);

    bool IsSupportedFile(string fileName);

    IReadOnlyList<string> SupportedExtensions { get; }
}

/// <summary>
/// Optional OCR provider. Implementations must never overwrite user-entered transaction fields;
/// callers apply results only to empty fields.
/// </summary>
public interface IOcrService
{
    bool IsAvailable { get; }

    Task<OcrExtractionResult?> ExtractAsync(string filePath, CancellationToken cancellationToken = default);

    Task<OcrExtractionResult?> ExtractBestAsync(
        IReadOnlyList<string> candidatePaths,
        CancellationToken cancellationToken = default);
}

public interface ICategoryService
{
    Task<IReadOnlyList<Category>> GetByTypeAsync(CategoryType type);

    Task<IReadOnlyList<Category>> GetAllActiveAsync();

    Task<IReadOnlyList<Category>> GetAllAsync(bool includeArchived = false);

    Task SaveAsync(Category category);

    Task ArchiveAsync(ObjectId id);

    Task RestoreAsync(ObjectId id);

    Task<bool> CanDeleteAsync(ObjectId id);

    Task DeleteAsync(ObjectId id);

    Task RenameAsync(ObjectId id, string newName);

    Task MergeAsync(ObjectId targetId, ObjectId sourceId);
}

public interface IFinancialYearService
{
    FinancialYear? GetCurrent();

    FinancialYear? GetById(ObjectId id);

    IReadOnlyList<FinancialYear> GetAll();

    IReadOnlyList<FinancialYearListItem> GetListItems();

    /// <summary>Year currently shown in the app (may be a prior year).</summary>
    FinancialYear GetActiveYear();

    /// <summary>Selects which year is shown in the app UI (does not change IsActive).</summary>
    void SetViewingYear(FinancialYear year);

    /// <summary>Ledger balance for one financial year = OpeningBalance + income − expenses.</summary>
    decimal GetCurrentBalance(ObjectId financialYearId);

    /// <summary>Closing balance for a year (same formula as current balance).</summary>
    decimal GetClosingBalance(ObjectId financialYearId);

    /// <summary>
    /// Club tracking start: bank balance at the end of the day the treasurer began using the app.
    /// Transactions on or before this date adjust opening balances only — not the current balance.
    /// </summary>
    (DateTime StartingDate, decimal StartingBalance) GetTrackingStart();

    /// <summary>
    /// True current bank balance = tracking start balance + net of transactions
    /// strictly after the tracking start date (across every financial year).
    /// </summary>
    decimal GetCurrentBankBalance();

    /// <summary>Recalculates opening balances for every financial year from the club tracking start.</summary>
    void RecalculateAllOpeningBalances();

    bool HasTransactions(ObjectId financialYearId);

    bool IsLocked(ObjectId financialYearId);

    /// <summary>
    /// Recalculates OpeningBalance from StartingBalance by reversing transactions
    /// on or before StartingDate. Safe to call after any import or ledger change.
    /// </summary>
    void RecalculateOpeningBalance(ObjectId financialYearId);

    /// <summary>
    /// Returns the financial year for a transaction date, creating it automatically
    /// when none exists. Transactions never manually belong to a year.
    /// </summary>
    FinancialYear EnsureYearForDate(DateTime date);

    /// <summary>
    /// Creates the next financial year from the active year end date, carries forward
    /// the closing balance, and archives the previous year. No manual configuration.
    /// </summary>
    Task<(bool Success, string? Error)> CreateNextYearAsync();

    Task<(bool Success, string? Error)> CreateAsync(CreateFinancialYearRequest request);

    /// <summary>
    /// Updates the user-entered starting date and bank balance, then recalculates OpeningBalance.
    /// </summary>
    Task<(bool Success, string? Error)> UpdateStartingBalanceAsync(
        ObjectId id, DateTime startingDate, decimal startingBalance, string notes);

    /// <summary>Legacy name — updates starting balance.</summary>
    Task<(bool Success, string? Error)> UpdateOpeningBalanceAsync(
        ObjectId id, DateTime openingDate, decimal openingBalance, string notes);

    Task<(bool Success, string? Error)> SetActiveAsync(ObjectId id);

    Task<(bool Success, string? Error)> ArchiveAsync(ObjectId id);

    Task<(bool Success, string? Error)> LockAsync(ObjectId id);

    Task<(bool Success, string? Error)> UnlockAsync(ObjectId id);

    Task<(bool Success, string? Error)> DeleteAsync(ObjectId id);

    event Action? ActiveYearChanged;

    event Action? YearsChanged;
}

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync();
}

public interface IReportService
{
    Task<MonthlyReportData> BuildMonthlyReportAsync(ObjectId financialYearId, int year, int month, string notes = "");

    Task<AgmReportData> BuildAgmReportAsync(ObjectId financialYearId);

    Task<string> ExportMonthlyPdfAsync(MonthlyReportData data, string outputPath);

    Task<string> ExportMonthlyExcelAsync(MonthlyReportData data, string outputPath);

    Task<string> ExportAgmPdfAsync(AgmReportData data, string outputPath);

    Task<string> ExportAgmExcelAsync(AgmReportData data, string outputPath);

    Task<string> ExportCategorySummaryExcelAsync(ObjectId financialYearId, string outputPath);

    Task<string> ExportGstSummaryExcelAsync(ObjectId financialYearId, string outputPath);

    Task<string> ExportReceiptAuditExcelAsync(ObjectId financialYearId, string outputPath);
}

public interface IBackupService
{
    Task<string> CreateBackupAsync(bool isManual = false);

    Task RestoreBackupAsync(string backupFilePath);

    Task BackupOnShutdownAsync();

    Task<IReadOnlyList<BackupInfo>> GetBackupHistoryAsync();

    /// <summary>Validates that a backup file exists and contains a usable database.</summary>
    void ValidateBackupFile(string backupFilePath);
}

public interface IDatabaseShutdownService
{
    void Close();
}

public interface IUserService
{
    Task<IReadOnlyList<User>> GetAllAsync();

    Task<User?> GetByIdAsync(ObjectId userId);

    Task<(bool Success, string? Error)> CreateAsync(string username, string displayName, string password, UserRole role, bool isActive);

    /// <summary>True when no active user already has this login username.</summary>
    bool IsUsernameAvailable(string username);

    Task<(bool Success, string? Error)> UpdateAsync(
        ObjectId userId,
        string username,
        string displayName,
        UserRole role,
        bool isActive,
        string? email,
        string? profilePicturePath,
        string? signatureImagePath);

    /// <summary>Updates only the digital signature for a user (self-service or admin).</summary>
    Task<(bool Success, string? Error)> UpdateSignatureAsync(ObjectId userId, string? signatureImagePath);

    Task<(bool Success, string? Error)> DeleteAsync(ObjectId userId);

    Task<(bool Success, string? Error)> SetActiveAsync(ObjectId userId, bool isActive);

    Task<(bool Success, string? Error)> UnlockAsync(ObjectId userId);
}

public interface IAuditService
{
    Task LogAsync(AuditAction action, ObjectId? targetUserId, string targetUsername, string details = "");

    Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int limit = 100);
}
