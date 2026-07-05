using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRCoder;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.ViewModels;

namespace NickeltownFinance.ViewModels.Dialogs;

public partial class MobileReceiptUploadViewModel : ViewModelBase
{
    private readonly IMobileUploadHost _uploadHost;
    private readonly DispatcherTimer _countdownTimer;
    private DateTime _expiresAtUtc;

    [ObservableProperty] private ImageSource? _qrCodeImage;
    [ObservableProperty] private string _uploadUrl = string.Empty;
    [ObservableProperty] private string _expiryCountdown = string.Empty;
    [ObservableProperty] private string _networkHint = string.Empty;
    [ObservableProperty] private bool _isSessionActive;

    private MobileUploadSessionRequest? _activeRequest;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;

    public event EventHandler<bool>? RequestClose;

    public MobileReceiptUploadViewModel(IMobileUploadHost uploadHost)
    {
        _uploadHost = uploadHost;
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) => UpdateCountdown();
    }

    public async Task InitializeAsync(MobileUploadSessionRequest? request = null)
    {
        if (request is not null)
            _activeRequest = request;
        else if (_isInitialized && IsSessionActive)
            return;

        await StartSessionInternalAsync(_activeRequest);
    }

    [RelayCommand]
    private void CopyLink()
    {
        if (string.IsNullOrWhiteSpace(UploadUrl)) return;

        try
        {
            System.Windows.Clipboard.SetText(UploadUrl);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not copy link: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshSessionAsync()
    {
        try
        {
            ErrorMessage = null;
            IsBusy = true;
            var session = await _uploadHost.RefreshSessionAsync(_activeRequest);
            ApplySession(session);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CloseSessionAsync()
    {
        _countdownTimer.Stop();
        try
        {
            await _uploadHost.StopAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        IsSessionActive = false;
        RequestClose?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, false);

    public async Task StopSessionSilentlyAsync()
    {
        _countdownTimer.Stop();
        if (!IsSessionActive) return;

        try
        {
            await _uploadHost.StopAsync();
        }
        catch
        {
            // shutting down
        }

        IsSessionActive = false;
    }

    private async Task StartSessionInternalAsync(MobileUploadSessionRequest? request = null)
    {
        try
        {
            ErrorMessage = null;
            IsBusy = true;
            var session = await _uploadHost.StartSessionAsync(request);
            ApplySession(session);
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not start mobile upload: {ex.Message}";
            IsSessionActive = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplySession(Core.DTOs.MobileUploadSessionInfo session)
    {
        UploadUrl = session.UploadUrl;
        _expiresAtUtc = session.ExpiresAt;
        NetworkHint = $"Same Wi‑Fi only · {session.LocalIpAddress}:{session.Port}";
        QrCodeImage = GenerateQrImage(session.QrPayload);
        IsSessionActive = true;
        UpdateCountdown();
        _countdownTimer.Start();
    }

    private void UpdateCountdown()
    {
        var remaining = _expiresAtUtc - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            ExpiryCountdown = "Expired";
            _countdownTimer.Stop();
            ErrorMessage = "Upload session expired. Refresh to generate a new QR code.";
            return;
        }

        ExpiryCountdown = remaining.Minutes > 0
            ? $"{remaining.Minutes}:{remaining.Seconds:D2}"
            : $"{remaining.Seconds}s";
    }

    private static ImageSource GenerateQrImage(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(12);

        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
