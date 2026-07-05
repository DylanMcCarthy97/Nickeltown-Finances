using OpenCvSharp;
using NickeltownFinance.Core.Helpers;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

public sealed class ReceiptThumbnailService : IReceiptThumbnailService
{
    public Task<string?> GenerateAsync(
        string sourceFilePath,
        string outputFilePath,
        int maxWidth = 120,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(sourceFilePath))
                return Task.FromResult<string?>(null);

            var ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".tif" or ".tiff" or ".heic"))
                return Task.FromResult<string?>(null);

            using var source = Cv2.ImRead(sourceFilePath, ImreadModes.Color);
            if (source.Empty())
                return Task.FromResult<string?>(null);

            var scale = maxWidth / (double)Math.Max(source.Cols, 1);
            var width = Math.Max(1, (int)(source.Cols * scale));
            var height = Math.Max(1, (int)(source.Rows * scale));

            using var thumb = new Mat();
            Cv2.Resize(source, thumb, new Size(width, height));

            Cv2.ImEncode(".jpg", thumb, out var bytes, new ImageEncodingParam(ImwriteFlags.JpegQuality, 80));
            if (bytes.Length == 0)
                return Task.FromResult<string?>(null);

            ReceiptPathHelper.WriteBytes(outputFilePath, bytes);
            return Task.FromResult<string?>(outputFilePath);
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }
}
