using OpenCvSharp;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Helpers;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

/// <summary>
/// Two-stage image pipeline: colour preview for users, grayscale pipeline for OCR only.
/// </summary>
public sealed class OpenCvReceiptImageProcessor : IReceiptImageProcessor
{
    public bool IsAvailable => true;

    public Task<ReceiptImageProcessResult> ProcessPreviewAsync(
        ReceiptImageProcessRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ProcessInternal(request, EnhancePreview));

    public Task<ReceiptImageProcessResult> ProcessOcrAsync(
        ReceiptImageProcessRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ProcessInternal(request, EnhanceForOcr));

    private static ReceiptImageProcessResult ProcessInternal(
        ReceiptImageProcessRequest request,
        Func<Mat, int?, Mat> enhance)
    {
        try
        {
            var sourcePath = request.SourceFilePath;
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return new ReceiptImageProcessResult
                {
                    Success = false,
                    ErrorMessage = $"Source file not found: {sourcePath}"
                };
            }

            using var source = Cv2.ImRead(sourcePath, ImreadModes.Color);
            if (source.Empty())
            {
                return new ReceiptImageProcessResult
                {
                    Success = false,
                    ErrorMessage = $"Unable to read image: {sourcePath}"
                };
            }

            using var processed = enhance(source, request.ManualRotationDegrees);
            Cv2.ImEncode(".jpg", processed, out var bytes, new ImageEncodingParam(ImwriteFlags.JpegQuality, 92));
            if (bytes.Length == 0)
            {
                return new ReceiptImageProcessResult
                {
                    Success = false,
                    ErrorMessage = "Failed to encode processed image."
                };
            }

            ReceiptPathHelper.WriteBytes(request.OutputFilePath, bytes);
            return new ReceiptImageProcessResult
            {
                Success = true,
                OutputFilePath = request.OutputFilePath
            };
        }
        catch (Exception ex)
        {
            return new ReceiptImageProcessResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>Colour preview: crop, deskew, exposure — never black-and-white.</summary>
    private static Mat EnhancePreview(Mat source, int? manualRotation)
    {
        using var rotated = ApplyRotation(source, manualRotation);
        using var portrait = AutoRotatePortrait(rotated);
        using var cropped = TryPerspectiveCorrect(portrait);
        using var balanced = BalanceColour(cropped);
        return SharpenMild(balanced);
    }

    /// <summary>OCR-only: grayscale, adaptive threshold, morphology.</summary>
    private static Mat EnhanceForOcr(Mat source, int? manualRotation)
    {
        using var rotated = ApplyRotation(source, manualRotation);
        using var portrait = AutoRotatePortrait(rotated);
        using var cropped = TryPerspectiveCorrect(portrait);
        using var gray = new Mat();
        Cv2.CvtColor(cropped, gray, ColorConversionCodes.BGR2GRAY);
        using var denoised = new Mat();
        Cv2.GaussianBlur(gray, denoised, new Size(3, 3), 0);
        using var binary = new Mat();
        Cv2.AdaptiveThreshold(
            denoised,
            binary,
            255,
            AdaptiveThresholdTypes.GaussianC,
            ThresholdTypes.Binary,
            31,
            10);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
        var output = new Mat();
        Cv2.MorphologyEx(binary, output, MorphTypes.Close, kernel);
        return output;
    }

    private static Mat ApplyRotation(Mat input, int? manualRotation)
    {
        if (manualRotation is not { } degrees || degrees % 360 == 0)
            return input.Clone();

        var center = new Point2f(input.Cols / 2f, input.Rows / 2f);
        var matrix = Cv2.GetRotationMatrix2D(center, degrees, 1.0);
        var outMat = new Mat();
        Cv2.WarpAffine(input, outMat, matrix, input.Size());
        return outMat;
    }

    private static Mat AutoRotatePortrait(Mat input)
    {
        if (input.Width <= input.Height)
            return input.Clone();

        var center = new Point2f(input.Cols / 2f, input.Rows / 2f);
        var matrix = Cv2.GetRotationMatrix2D(center, -90, 1.0);
        var rotated = new Mat();
        Cv2.WarpAffine(input, rotated, matrix, new Size(input.Rows, input.Cols));
        return rotated;
    }

    private static Mat BalanceColour(Mat input)
    {
        var output = new Mat();
        using var lab = new Mat();
        Cv2.CvtColor(input, lab, ColorConversionCodes.BGR2Lab);
        var channels = Cv2.Split(lab);
        using var clahe = Cv2.CreateCLAHE(clipLimit: 1.8, tileGridSize: new Size(8, 8));
        clahe.Apply(channels[0], channels[0]);
        Cv2.Merge(channels, lab);
        Cv2.CvtColor(lab, output, ColorConversionCodes.Lab2BGR);

        // Lift shadows and compress highlights while keeping colour.
        output.ConvertTo(output, MatType.CV_32FC3, 1.0 / 255.0);
        Cv2.Pow(output, 0.92, output);
        output.ConvertTo(output, MatType.CV_8UC3, 255.0);

        foreach (var channel in channels)
            channel.Dispose();

        return output;
    }

    private static Mat SharpenMild(Mat input)
    {
        using var blurred = new Mat();
        Cv2.GaussianBlur(input, blurred, new Size(0, 0), 1.2);
        var output = new Mat();
        Cv2.AddWeighted(input, 1.15, blurred, -0.15, 0, output);
        return output;
    }

    private static Mat TryPerspectiveCorrect(Mat input)
    {
        using var resized = ResizeForProcessing(input, maxDim: 1200);
        using var gray = new Mat();
        Cv2.CvtColor(resized, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, gray, new Size(5, 5), 0);
        using var edges = new Mat();
        Cv2.Canny(gray, edges, 50, 150);

        Cv2.FindContours(edges, out var contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
        var scaleX = (double)input.Cols / resized.Cols;
        var scaleY = (double)input.Rows / resized.Rows;

        Point2f[]? best = null;
        double bestArea = 0;
        foreach (var contour in contours.OrderByDescending(c => Cv2.ContourArea(c)).Take(8))
        {
            var peri = Cv2.ArcLength(contour, true);
            var approx = Cv2.ApproxPolyDP(contour, 0.02 * peri, true);
            if (approx.Length != 4) continue;
            var area = Cv2.ContourArea(approx);
            if (area <= bestArea) continue;
            bestArea = area;
            best = approx.Select(p => new Point2f((float)(p.X * scaleX), (float)(p.Y * scaleY))).ToArray();
        }

        if (best is null || bestArea < input.Total() * 0.15)
            return input.Clone();

        var ordered = OrderPoints(best);
        var width = (int)Math.Max(
            Distance(ordered[0], ordered[1]),
            Distance(ordered[2], ordered[3]));
        var height = (int)Math.Max(
            Distance(ordered[0], ordered[3]),
            Distance(ordered[1], ordered[2]));
        if (width < 100 || height < 100) return input.Clone();

        var dst = new[]
        {
            new Point2f(0, 0),
            new Point2f(width - 1, 0),
            new Point2f(width - 1, height - 1),
            new Point2f(0, height - 1)
        };

        using var matrix = Cv2.GetPerspectiveTransform(ordered, dst);
        var warped = new Mat();
        Cv2.WarpPerspective(input, warped, matrix, new Size(width, height));
        return warped;
    }

    private static Mat ResizeForProcessing(Mat input, int maxDim)
    {
        var max = Math.Max(input.Cols, input.Rows);
        if (max <= maxDim) return input.Clone();
        var scale = maxDim / (double)max;
        var resized = new Mat();
        Cv2.Resize(input, resized, new Size((int)(input.Cols * scale), (int)(input.Rows * scale)));
        return resized;
    }

    private static Point2f[] OrderPoints(Point2f[] pts)
    {
        var ordered = new Point2f[4];
        var sum = pts.Select(p => p.X + p.Y).ToArray();
        var diff = pts.Select(p => p.X - p.Y).ToArray();
        ordered[0] = pts[Array.IndexOf(sum, sum.Min())];
        ordered[2] = pts[Array.IndexOf(sum, sum.Max())];
        ordered[1] = pts[Array.IndexOf(diff, diff.Min())];
        ordered[3] = pts[Array.IndexOf(diff, diff.Max())];
        return ordered;
    }

    private static double Distance(Point2f a, Point2f b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
