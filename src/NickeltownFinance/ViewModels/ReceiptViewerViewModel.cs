using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NickeltownFinance.Converters;
using NickeltownFinance.Core.DTOs;

namespace NickeltownFinance.ViewModels;

public enum ReceiptPreviewViewMode
{
    Enhanced,
    Original,
    OcrDebug
}

public sealed class PageThumbnailItem
{
    public int PageNumber { get; init; }

    public string PreviewPath { get; init; } = string.Empty;

    public ImageSource? Thumbnail { get; set; }
}

public partial class ReceiptViewerViewModel : ViewModelBase
{
    private readonly List<AttachmentInfo> _attachments = [];
    private List<string> _currentPagePaths = [];
    private int _attachmentIndex;

    [ObservableProperty] private AttachmentInfo? _current;
    [ObservableProperty] private ImageSource? _imageSource;
    [ObservableProperty] private double _zoom = 1.0;
    [ObservableProperty] private double _rotation;
    [ObservableProperty] private double _panX;
    [ObservableProperty] private double _panY;
    [ObservableProperty] private string _title = "Document Viewer";
    [ObservableProperty] private string _positionText = string.Empty;
    [ObservableProperty] private string _pagePositionText = string.Empty;
    [ObservableProperty] private int _currentPageIndex;
    [ObservableProperty] private int _pageCount = 1;
    [ObservableProperty] private bool _hasMultiplePages;
    [ObservableProperty] private bool _hasRenderableContent;
    [ObservableProperty] private bool _isPdf;
    [ObservableProperty] private ReceiptPreviewViewMode _selectedViewMode = ReceiptPreviewViewMode.Enhanced;

    public bool ShowOcrDebugTab => Debugger.IsAttached;

    public bool ShowViewModeTabs => Current is not null && !HasMultiplePages && !IsPdf;

    private string? _enhancedPath;
    private string? _originalPath;
    private string? _ocrPath;

    public ObservableCollection<PageThumbnailItem> PageThumbnails { get; } = [];

    public event EventHandler? RequestClose;

    public void Load(IReadOnlyList<AttachmentInfo> attachments, int startIndex = 0)
    {
        _attachments.Clear();
        _attachments.AddRange(attachments);
        if (_attachments.Count == 0) return;
        ShowAttachment(Math.Clamp(startIndex, 0, _attachments.Count - 1));
    }

    public void LoadFromInbox(ReceiptImportItemInfo item)
    {
        var previews = item.PreviewFullPaths.Count > 0
            ? item.PreviewFullPaths
            : !string.IsNullOrWhiteSpace(item.ProcessedFullPath)
                ? [item.ProcessedFullPath]
                : [];

        var info = new AttachmentInfo
        {
            FileName = item.FileName,
            FullPath = item.OriginalFullPath,
            ThumbnailFullPath = item.ThumbnailFullPath,
            IsPdf = Path.GetExtension(item.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase),
            IsImage = previews.Count > 0 || IsImageExtension(item.FileName),
            PageCount = item.PageCount > 0 ? item.PageCount : Math.Max(1, previews.Count),
            PreviewFullPaths = previews,
            OcrImageFullPath = item.OcrImageFullPath
        };

        Load([info]);
    }

    partial void OnSelectedViewModeChanged(ReceiptPreviewViewMode value) => LoadCurrentPage();

    [RelayCommand]
    private void SetViewMode(string? mode)
    {
        if (!Enum.TryParse<ReceiptPreviewViewMode>(mode, ignoreCase: true, out var parsed))
            return;

        if (parsed == ReceiptPreviewViewMode.OcrDebug && !ShowOcrDebugTab)
            return;

        SelectedViewMode = parsed;
    }

    partial void OnCurrentPageIndexChanged(int value) => LoadCurrentPage();

    private void ShowAttachment(int index)
    {
        _attachmentIndex = index;
        Current = _attachments[index];
        Title = Current.FileName;
        PositionText = $"{index + 1} / {_attachments.Count}";
        IsPdf = Current.IsPdf;
        Zoom = 1.0;
        Rotation = 0;
        PanX = 0;
        PanY = 0;
        CurrentPageIndex = 0;

        _currentPagePaths = BuildPagePaths(Current);
        _originalPath = Current.FullPath;
        _enhancedPath = Current.PreviewFullPaths.FirstOrDefault() ?? Current.DisplayPreviewPath;
        _ocrPath = Current.OcrImageFullPath;
        SelectedViewMode = ReceiptPreviewViewMode.Enhanced;
        OnPropertyChanged(nameof(ShowViewModeTabs));
        PageCount = Math.Max(1, _currentPagePaths.Count > 0 ? _currentPagePaths.Count : Current.PageCount);
        HasMultiplePages = PageCount > 1;
        HasRenderableContent = !string.IsNullOrWhiteSpace(ResolveDisplayPath()) && File.Exists(ResolveDisplayPath()!);

        BuildPageThumbnails();
        LoadCurrentPage();
        UpdatePagePositionText();
    }

    private void BuildPageThumbnails()
    {
        PageThumbnails.Clear();
        if (_currentPagePaths.Count <= 1)
            return;

        for (var i = 0; i < _currentPagePaths.Count; i++)
        {
            var path = _currentPagePaths[i];
            ImageSource? thumb = null;
            if (File.Exists(path))
            {
                try { thumb = ImageLoadHelper.LoadUnlocked(path); } catch { /* ignore */ }
            }

            PageThumbnails.Add(new PageThumbnailItem
            {
                PageNumber = i + 1,
                PreviewPath = path,
                Thumbnail = thumb
            });
        }
    }

    private void LoadCurrentPage()
    {
        var path = ResolveDisplayPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ImageSource = null;
            return;
        }

        try
        {
            ImageSource = ImageLoadHelper.LoadUnlocked(path);
        }
        catch
        {
            ImageSource = null;
        }

        UpdatePagePositionText();
    }

    private string? ResolveDisplayPath()
    {
        if (HasMultiplePages && _currentPagePaths.Count > 0)
        {
            var index = Math.Clamp(CurrentPageIndex, 0, _currentPagePaths.Count - 1);
            return _currentPagePaths[index];
        }

        return SelectedViewMode switch
        {
            ReceiptPreviewViewMode.Original => _originalPath,
            ReceiptPreviewViewMode.OcrDebug => _ocrPath ?? _enhancedPath ?? _originalPath,
            _ => _enhancedPath ?? _originalPath
        };
    }

    private static List<string> BuildPagePaths(AttachmentInfo attachment)
    {
        if (attachment.PreviewFullPaths.Count > 0)
            return attachment.PreviewFullPaths.Where(File.Exists).ToList();

        if (attachment.IsImage && File.Exists(attachment.FullPath))
            return [attachment.FullPath];

        if (!string.IsNullOrWhiteSpace(attachment.DisplayPreviewPath) && File.Exists(attachment.DisplayPreviewPath))
            return [attachment.DisplayPreviewPath];

        return [];
    }

    private void UpdatePagePositionText()
    {
        PagePositionText = HasMultiplePages
            ? $"Page {CurrentPageIndex + 1} / {PageCount}"
            : string.Empty;
    }

    [RelayCommand]
    private void NextAttachment()
    {
        if (_attachments.Count == 0) return;
        if (_attachmentIndex < _attachments.Count - 1)
            ShowAttachment(_attachmentIndex + 1);
    }

    [RelayCommand]
    private void PreviousAttachment()
    {
        if (_attachments.Count == 0) return;
        if (_attachmentIndex > 0)
            ShowAttachment(_attachmentIndex - 1);
    }

    [RelayCommand]
    private void NextPage()
    {
        if (!HasMultiplePages) return;
        if (CurrentPageIndex < _currentPagePaths.Count - 1)
        {
            CurrentPageIndex++;
            ResetPan();
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (!HasMultiplePages) return;
        if (CurrentPageIndex > 0)
        {
            CurrentPageIndex--;
            ResetPan();
        }
    }

    [RelayCommand]
    private void SelectPage(int pageNumber)
    {
        if (pageNumber < 1)
            return;

        CurrentPageIndex = Math.Clamp(pageNumber - 1, 0, Math.Max(0, _currentPagePaths.Count - 1));
        ResetPan();
    }

    public void AdjustZoom(double delta)
    {
        Zoom = Math.Clamp(Zoom + delta, 0.1, 8);
    }

    [RelayCommand]
    private void ZoomIn() => AdjustZoom(0.25);

    [RelayCommand]
    private void ZoomOut() => AdjustZoom(-0.25);

    [RelayCommand]
    private void FitToWidth() => Zoom = 1.0;

    [RelayCommand]
    private void FitToHeight() => Zoom = 0.9;

    [RelayCommand]
    private void FitToPage() => Zoom = 0.85;

    [RelayCommand]
    private void Rotate()
    {
        Rotation = (Rotation + 90) % 360;
        ResetPan();
    }

    public void PanBy(double deltaX, double deltaY)
    {
        PanX += deltaX;
        PanY += deltaY;
    }

    public void ResetPan()
    {
        PanX = 0;
        PanY = 0;
    }

    [RelayCommand]
    private void OpenOriginal()
    {
        if (Current is null || !File.Exists(Current.FullPath)) return;
        Process.Start(new ProcessStartInfo(Current.FullPath) { UseShellExecute = true });
    }

    [RelayCommand]
    private void SaveAs()
    {
        if (Current is null || !File.Exists(Current.FullPath)) return;

        var dialog = new SaveFileDialog
        {
            FileName = Current.FileName,
            Filter = "All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;
        File.Copy(Current.FullPath, dialog.FileName, overwrite: true);
    }

    [RelayCommand]
    private void Print()
    {
        var printPath = ResolveDisplayPath();
        if (string.IsNullOrWhiteSpace(printPath) || !File.Exists(printPath))
        {
            if (Current is null || !File.Exists(Current.FullPath)) return;
            printPath = Current.FullPath;
        }

        var psi = new ProcessStartInfo(printPath)
        {
            UseShellExecute = true,
            Verb = "print"
        };
        try
        {
            Process.Start(psi);
        }
        catch
        {
            OpenOriginal();
        }
    }

    [RelayCommand]
    private void Download()
    {
        SaveAs();
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke(this, EventArgs.Empty);

    private static bool IsImageExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".tif" or ".tiff" or ".heic";
    }
}
