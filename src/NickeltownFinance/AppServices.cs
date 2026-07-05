using Microsoft.Extensions.DependencyInjection;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Infrastructure;
using NickeltownFinance.Services;
using NickeltownFinance.ViewModels;
using Serilog;

namespace NickeltownFinance;

public static class AppServices
{
    public static IServiceProvider Configure()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(Core.Constants.AppPaths.LogsPath, "nickeltown-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddSerilog(dispose: true));
        services.AddNickeltownInfrastructure();
        services.AddSingleton<IOcrService, Services.Ocr.WindowsMediaOcrService>();

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IWindowStateService, WindowStateService>();
        services.AddSingleton<IAppUpdateService, AppUpdateService>();

        services.AddSingleton<ReceiptImportNotificationBridge>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<SetupWizardViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<TransactionsViewModel>();
        services.AddTransient<ImportViewModel>();
        services.AddTransient<ReceiptInboxViewModel>();
        services.AddTransient<ReconciliationViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<MonthlyReportViewModel>();
        services.AddTransient<AgmReportViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<UserManagementViewModel>();
        services.AddTransient<FinancialYearsViewModel>();
        services.AddTransient<TransactionEditorViewModel>();
        services.AddTransient<ReceiptViewerViewModel>();
        services.AddTransient<ViewModels.Dialogs.MobileReceiptUploadViewModel>();
        services.AddTransient<ViewModels.Dialogs.NewUserDialogViewModel>();
        services.AddTransient<ViewModels.Dialogs.ResetPasswordDialogViewModel>();
        services.AddTransient<ViewModels.Dialogs.NewFinancialYearDialogViewModel>();
        services.AddTransient<ViewModels.Dialogs.EditOpeningBalanceDialogViewModel>();

        services.AddTransient<Views.LoginWindow>();
        services.AddTransient<Views.SetupWizardWindow>();
        services.AddTransient<Views.MainWindow>();
        services.AddTransient<Views.Dialogs.MobileReceiptUploadWindow>();
        services.AddTransient<Views.Dialogs.TransactionEditorWindow>();
        services.AddTransient<Views.Dialogs.ReceiptViewerWindow>();
        services.AddTransient<Views.Dialogs.NewUserDialogWindow>();
        services.AddTransient<Views.Dialogs.ResetPasswordDialogWindow>();
        services.AddTransient<Views.Dialogs.NewFinancialYearDialogWindow>();
        services.AddTransient<Views.Dialogs.EditOpeningBalanceDialogWindow>();



        return services.BuildServiceProvider();
    }
}
