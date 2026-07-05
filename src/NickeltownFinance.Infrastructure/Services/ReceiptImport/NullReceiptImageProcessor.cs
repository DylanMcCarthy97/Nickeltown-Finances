using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

public sealed class NullReceiptImageProcessor : IReceiptImageProcessor
{
    public bool IsAvailable => false;

    public Task<ReceiptImageProcessResult> ProcessPreviewAsync(
        ReceiptImageProcessRequest request,
        CancellationToken cancellationToken = default) =>
        Unavailable(request);

    public Task<ReceiptImageProcessResult> ProcessOcrAsync(
        ReceiptImageProcessRequest request,
        CancellationToken cancellationToken = default) =>
        Unavailable(request);

    private static Task<ReceiptImageProcessResult> Unavailable(ReceiptImageProcessRequest request) =>
        Task.FromResult(new ReceiptImageProcessResult
        {
            Success = false,
            OutputFilePath = request.OutputFilePath,
            ErrorMessage = "Receipt image processing is not configured."
        });
}
