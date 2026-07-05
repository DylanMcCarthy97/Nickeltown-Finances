using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Services;
using NickeltownFinance.ViewModels.Dialogs;
using NickeltownFinance.Views.Dialogs;

namespace NickeltownFinance.ViewModels;

public partial class FinancialYearsViewModel : ViewModelBase
{
    private readonly IFinancialYearService _financialYearService;
    private readonly ISessionService _sessionService;
    private readonly INotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty] private ObservableCollection<FinancialYearListItem> _years = [];
    [ObservableProperty] private FinancialYearListItem? _selectedYear;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private bool _canManage;
    [ObservableProperty] private bool _canEditSelected;
    [ObservableProperty] private bool _canDeleteSelected;

    public FinancialYearsViewModel(
        IFinancialYearService financialYearService,
        ISessionService sessionService,
        INotificationService notificationService,
        IServiceProvider serviceProvider)
    {
        _financialYearService = financialYearService;
        _sessionService = sessionService;
        _notificationService = notificationService;
        _serviceProvider = serviceProvider;

        var role = _sessionService.CurrentUser?.Role;
        CanManage = role is UserRole.Administrator or UserRole.Treasurer;

        _financialYearService.YearsChanged += OnYearsChanged;
        _ = LoadAsync();
    }

    partial void OnSelectedYearChanged(FinancialYearListItem? value)
    {
        HasSelection = value is not null;
        CanEditSelected = value is not null && CanManage;
        CanDeleteSelected = value is { CanDelete: true } && CanManage;
    }

    private void OnYearsChanged() => _ = LoadAsync();

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var selectedId = SelectedYear?.Id;
            var items = _financialYearService.GetListItems();
            Years = new ObservableCollection<FinancialYearListItem>(items);
            SelectedYear = Years.FirstOrDefault(y => y.Id == selectedId) ?? Years.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void GenerateAgmReports()
    {
        _serviceProvider.GetRequiredService<INavigationService>().Navigate<ReportsViewModel>();
        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel main
            && main.CurrentPage is ReportsViewModel reports)
            reports.ShowAgmReport();
    }

    [RelayCommand]
    private async Task NewFinancialYearAsync()
    {
        if (!CanManage)
        {
            AppDialog.Error("Permission denied", "Only the Treasurer or Administrator can create financial years.");
            return;
        }

        var current = _financialYearService.GetCurrent()
                      ?? _financialYearService.GetAll().FirstOrDefault();
        if (current is null)
        {
            AppDialog.Error("No financial year", "Complete first-run setup before creating a new year.");
            return;
        }

        var nextOpening = current.EndDate.Date.AddDays(1);
        var closing = _financialYearService.GetClosingBalance(current.Id);
        if (!AppDialog.Confirm("Create new financial year",
                $"Create the next financial year starting {nextOpening:dd/MM/yyyy}?\n\n" +
                $"Closing balance from {current.Name} ({closing:C}) will be carried forward, " +
                "and the previous year will be archived."))
            return;

        IsBusy = true;
        try
        {
            var (success, error) = await _financialYearService.CreateNextYearAsync();
            if (!success)
            {
                AppDialog.Error("Unable to create year", error ?? "Unknown error.");
                return;
            }

            _notificationService.ShowSuccess("New financial year created. Previous year archived.");
            RefreshMainWindowYears();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedYear is null) return;

        if (!CanManage)
        {
            AppDialog.Error("Permission denied", "Only the Treasurer or Administrator can edit the starting balance.");
            return;
        }

        if (SelectedYear.IsLocked)
        {
            AppDialog.Error("Year locked", "Unlock this financial year before editing the starting balance.");
            return;
        }

        var vm = _serviceProvider.GetRequiredService<EditOpeningBalanceDialogViewModel>();
        vm.Initialize(SelectedYear);
        var window = new EditOpeningBalanceDialogWindow(vm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (window.ShowDialog() == true)
        {
            _notificationService.ShowSuccess("Starting balance updated. Opening balance and reports recalculated.");
            RefreshMainWindowYears();
        }
    }

    [RelayCommand]
    private async Task LockAsync()
    {
        if (SelectedYear is null || !CanManage) return;

        if (SelectedYear.IsLocked)
        {
            var (unlockOk, unlockError) = await _financialYearService.UnlockAsync(SelectedYear.Id);
            if (!unlockOk)
            {
                AppDialog.Error("Unable to unlock", unlockError ?? "Unknown error.");
                return;
            }

            _notificationService.ShowSuccess($"Financial year {SelectedYear.Name} unlocked.");
            RefreshMainWindowYears();
            return;
        }

        if (!AppDialog.Confirm("Lock financial year",
                $"Lock financial year {SelectedYear.Name}? Transactions and imports will be blocked."))
            return;

        var (success, error) = await _financialYearService.LockAsync(SelectedYear.Id);
        if (!success)
        {
            AppDialog.Error("Unable to lock", error ?? "Unknown error.");
            return;
        }

        _notificationService.ShowSuccess($"Financial year {SelectedYear.Name} locked.");
        RefreshMainWindowYears();
    }

    [RelayCommand]
    private async Task SetActiveAsync()
    {
        if (SelectedYear is null) return;

        if (!CanManage)
        {
            AppDialog.Error("Permission denied", "Only the Treasurer or Administrator can change the active financial year.");
            return;
        }

        if (SelectedYear.IsActive)
        {
            _notificationService.ShowSuccess("This financial year is already active.");
            return;
        }

        var (success, error) = await _financialYearService.SetActiveAsync(SelectedYear.Id);
        if (!success)
        {
            AppDialog.Error("Unable to set active", error ?? "Unknown error.");
            return;
        }

        _notificationService.ShowSuccess($"Financial year {SelectedYear.Name} is now active.");
        RefreshMainWindowYears();
    }

    [RelayCommand]
    private async Task ArchiveAsync()
    {
        if (SelectedYear is null) return;

        if (!CanManage)
        {
            AppDialog.Error("Permission denied", "Only the Treasurer or Administrator can archive financial years.");
            return;
        }

        if (SelectedYear.IsArchived)
        {
            AppDialog.Info("Already archived", "This financial year is already archived.");
            return;
        }

        if (!AppDialog.Confirm("Archive financial year",
                $"Archive financial year {SelectedYear.Name}? It will no longer be the active year."))
            return;

        var (success, error) = await _financialYearService.ArchiveAsync(SelectedYear.Id);
        if (!success)
        {
            AppDialog.Error("Unable to archive", error ?? "Unknown error.");
            return;
        }

        _notificationService.ShowSuccess($"Financial year {SelectedYear.Name} archived.");
        RefreshMainWindowYears();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedYear is null) return;

        if (!CanManage)
        {
            AppDialog.Error("Permission denied", "Only the Treasurer or Administrator can delete financial years.");
            return;
        }

        if (!SelectedYear.CanDelete)
        {
            AppDialog.Error("Cannot delete", "This financial year has transactions and cannot be deleted.");
            return;
        }

        if (!AppDialog.Confirm("Delete financial year",
                $"Permanently delete financial year {SelectedYear.Name}? This cannot be undone."))
            return;

        var (success, error) = await _financialYearService.DeleteAsync(SelectedYear.Id);
        if (!success)
        {
            AppDialog.Error("Unable to delete", error ?? "Unknown error.");
            return;
        }

        _notificationService.ShowSuccess("Financial year deleted.");
        RefreshMainWindowYears();
    }

    private void RefreshMainWindowYears()
    {
        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel main)
            main.RefreshFinancialYears();
    }
}
