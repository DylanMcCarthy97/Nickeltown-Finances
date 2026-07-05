using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Services;
using NickeltownFinance.Views;
using Serilog;

namespace NickeltownFinance;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Set before shutdown following a backup restore so we do not back up a closed database.</summary>
    public static bool SkipShutdownBackup { get; set; }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Login is a dialog; without this, closing it shuts the app down before MainWindow opens.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            Directory.CreateDirectory(AppPaths.LogsPath);
            Services = AppServices.Configure();

            var seeder = Services.GetRequiredService<IDataSeederService>();
            seeder.SeedIfNeeded();

            var settings = Services.GetRequiredService<ISettingsService>();
            ClubBranding.EnsureSettingsLogo(settings);

            var themeService = Services.GetRequiredService<IThemeService>();
            themeService.ApplyTheme(settings.Theme);

            // First-run setup: club name, financial year start, bank balance, starting date.
            if (seeder.IsSetupRequired())
            {
                var setup = Services.GetRequiredService<SetupWizardWindow>();
                if (setup.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }

                ClubBranding.EnsureSettingsLogo(settings);
                themeService.ApplyTheme(settings.Theme);
            }

            var login = Services.GetRequiredService<LoginWindow>();
            var loggedIn = login.ShowDialog() == true;

            if (!loggedIn)
            {
                Shutdown();
                return;
            }

            var main = Services.GetRequiredService<MainWindow>();
            MainWindow = main;
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            _ = Services.GetRequiredService<ReceiptImportNotificationBridge>();

            main.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application failed to start");
            try
            {
                AppDialog.Error("Startup error", $"Nickeltown Finance failed to start:\n\n{ex.Message}");
            }
            catch
            {
                MessageBox.Show(
                    $"Nickeltown Finance failed to start:\n\n{ex.Message}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            Shutdown();

        }
    }

    /// <summary>
    /// Clears the current session and shows the login screen so another user can sign in.
    /// </summary>
    public void LogoutAndSwitchUser()
    {
        var session = Services.GetRequiredService<ISessionService>();
        session.Clear();

        // Closing the main window must not exit the app while we show login again.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var oldMain = MainWindow;
        MainWindow = null;
        oldMain?.Close();

        var login = Services.GetRequiredService<LoginWindow>();
        var loggedIn = login.ShowDialog() == true;

        if (!loggedIn)
        {
            Shutdown();
            return;
        }

        var main = Services.GetRequiredService<MainWindow>();
        MainWindow = main;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        main.Show();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        try
        {
            if (Services is not null)
            {
                var host = Services.GetService<IMobileUploadHost>();
                if (host is IAsyncDisposable asyncHost)
                    asyncHost.DisposeAsync().AsTask().GetAwaiter().GetResult();
                else
                    host?.StopAsync().GetAwaiter().GetResult();

                if (!SkipShutdownBackup)
                {
                    var backup = Services.GetRequiredService<IBackupService>();
                    backup.BackupOnShutdownAsync().GetAwaiter().GetResult();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Backup on shutdown failed");
        }

        if (Services is IAsyncDisposable asyncServices)
            asyncServices.DisposeAsync().AsTask().GetAwaiter().GetResult();
        else if (Services is IDisposable disposable)
            disposable.Dispose();

        Log.CloseAndFlush();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception");
        try
        {
            AppDialog.Error("Unexpected error", $"An unexpected error occurred:\n\n{e.Exception.Message}");
        }
        catch
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}",
                "Nickeltown Finance",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        e.Handled = true;

    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Log.Fatal(ex, "Unhandled exception");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}
