using System.Windows;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Services;

/// <summary>
/// Shows desktop notifications when receipts arrive from mobile upload or finish processing.
/// Must marshal to the UI thread — WPF snackbar requires STA.
/// </summary>
public sealed class ReceiptImportNotificationBridge : IDisposable
{
    private readonly IReceiptImportQueue _queue;
    private readonly INotificationService _notifications;
    private readonly HashSet<string> _notifiedKeys = new();

    public ReceiptImportNotificationBridge(
        IReceiptImportQueue queue,
        INotificationService notifications)
    {
        _queue = queue;
        _notifications = notifications;
        _queue.ItemUpdated += OnItemUpdated;
    }

    private void OnItemUpdated(object? sender, ReceiptImportItemInfo item)
    {
        if (item.Source != ReceiptImportSource.Mobile)
            return;

        if (item.ImportTarget != ReceiptImportTarget.Inbox)
            return;

        var key = $"{item.Id}:{item.Status}";
        lock (_notifiedKeys)
        {
            if (!_notifiedKeys.Add(key))
                return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ShowNotification(item);
            return;
        }

        dispatcher.BeginInvoke(() => ShowNotification(item));
    }

    private void ShowNotification(ReceiptImportItemInfo item)
    {
        if (item.Status is ReceiptImportStatus.Queued or ReceiptImportStatus.Uploading)
        {
            _notifications.ShowInfo($"Receipt received: {item.FileName}");
            return;
        }

        if (item.Status is ReceiptImportStatus.Ready or ReceiptImportStatus.CompletedWithWarnings)
            _notifications.ShowSuccess($"Receipt ready for review: {item.FileName}");
    }

    public void Dispose() => _queue.ItemUpdated -= OnItemUpdated;
}
