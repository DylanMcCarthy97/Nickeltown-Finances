using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

public sealed class NullScannerService : IScannerService
{
    public bool IsAvailable => false;

    public Task<IReadOnlyList<ScannerDeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ScannerDeviceInfo>>(Array.Empty<ScannerDeviceInfo>());

    public Task<ScannerCaptureResult> ScanAsync(
        ScannerCaptureRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromException<ScannerCaptureResult>(
            new NotSupportedException("Scanner capture is not configured. TWAIN/WIA support is planned."));
}
