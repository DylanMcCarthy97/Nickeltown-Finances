using Microsoft.Extensions.DependencyInjection;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Infrastructure.Database;
using NickeltownFinance.Infrastructure.Repositories;
using NickeltownFinance.Infrastructure.Services;
using NickeltownFinance.Infrastructure.Services.Document;
using NickeltownFinance.Infrastructure.Services.ReceiptImport;

namespace NickeltownFinance.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNickeltownInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<LiteDbContext>();
        services.AddSingleton<IDatabaseShutdownService, DatabaseShutdownService>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IAuditLogRepository, AuditLogRepository>();
        services.AddSingleton<ICategoryRepository, CategoryRepository>();
        services.AddSingleton<IFinancialYearRepository, FinancialYearRepository>();
        services.AddSingleton<ITransactionRepository, TransactionRepository>();
        services.AddSingleton<IAttachmentRepository, AttachmentRepository>();
        services.AddSingleton<ICategorisationRuleRepository, CategorisationRuleRepository>();
        services.AddSingleton<IImportBatchRepository, ImportBatchRepository>();
        services.AddSingleton<ISquareDepositRepository, SquareDepositRepository>();
        services.AddSingleton<ISquareTransactionRepository, SquareTransactionRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();

        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDataSeederService, DataSeederService>();
        services.AddSingleton<IFinancialYearService, FinancialYearService>();
        services.AddSingleton<ITransactionService, TransactionService>();
        services.AddSingleton<ICategoryService, CategoryService>();
        services.AddSingleton<IDashboardService, DashboardService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<ICategorisationService, CategorisationService>();
        services.AddSingleton<IReconciliationService, ReconciliationService>();
        services.AddSingleton<IStatementImportService, StatementImportService>();
        services.AddSingleton<ISquareImportService, SquareImportService>();
        services.AddSingleton<IAttachmentService, AttachmentService>();

        services.AddSingleton<IReceiptImportItemRepository, ReceiptImportItemRepository>();
        services.AddSingleton<IReceiptImportBatchRepository, ReceiptImportBatchRepository>();
        services.AddSingleton<ISupplierProfileRepository, SupplierProfileRepository>();
        services.AddSingleton<IReceiptImportQueue, ReceiptImportQueue>();
        services.AddSingleton<IReceiptImportService, ReceiptImportService>();
        services.AddSingleton<IMobileUploadHost, MobileUploadHost>();
        services.AddSingleton<IReceiptImageProcessor, OpenCvReceiptImageProcessor>();
        services.AddSingleton<IReceiptOcrFieldParser, ReceiptOcrFieldParser>();
        services.AddSingleton<ISupplierDetectionService, SupplierDetectionService>();
        services.AddSingleton<IReceiptAiParser, RuleBasedReceiptAiParser>();
        services.AddSingleton<IReceiptMatchingService, ReceiptMatchingService>();
        services.AddSingleton<IReceiptThumbnailService, ReceiptThumbnailService>();
        services.AddSingleton<IReceiptDuplicateDetector, ReceiptDuplicateDetector>();
        services.AddSingleton<IReceiptProcessingSettingsService, ReceiptProcessingSettingsService>();
        services.AddSingleton<IReceiptProcessingLogger, ReceiptProcessingLogger>();
        services.AddSingleton<IScannerService, NullScannerService>();
        services.AddSingleton<IReceiptImportBatchService, ReceiptImportBatchService>();
        services.AddSingleton<IPdfRenderService, PdfRenderService>();
        services.AddSingleton<IDocumentPreviewService, DocumentPreviewService>();
        services.AddSingleton<IGitHubReleaseClient, GitHubReleaseClient>();

        return services;

    }
}
