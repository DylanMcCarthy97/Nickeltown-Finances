using System.Windows;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Services;

public interface IWindowStateService
{
    void Restore(Window window);

    void Save(Window window);
}

public class WindowStateService : IWindowStateService
{
    private const double MinWidth = 900;
    private const double MinHeight = 600;

    private readonly ISettingsService _settingsService;

    public WindowStateService(ISettingsService settingsService) => _settingsService = settingsService;

    public void Restore(Window window)
    {
        var workArea = SystemParameters.WorkArea;
        var width = _settingsService.WindowWidth ?? Math.Min(1400, workArea.Width * 0.92);
        var height = _settingsService.WindowHeight ?? Math.Min(860, workArea.Height * 0.92);

        width = Math.Clamp(width, MinWidth, workArea.Width);
        height = Math.Clamp(height, MinHeight, workArea.Height);

        window.MinWidth = MinWidth;
        window.MinHeight = MinHeight;
        window.Width = width;
        window.Height = height;

        if (_settingsService.WindowLeft is { } left && _settingsService.WindowTop is { } top)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = left;
            window.Top = top;
            EnsureOnScreen(window);
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (Enum.TryParse<WindowState>(_settingsService.WindowState, out var state) &&
            state is WindowState.Normal or WindowState.Maximized)
        {
            window.WindowState = state;
        }
    }

    public void Save(Window window)
    {
        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;

        _settingsService.WindowLeft = bounds.Left;
        _settingsService.WindowTop = bounds.Top;
        _settingsService.WindowWidth = bounds.Width;
        _settingsService.WindowHeight = bounds.Height;
        _settingsService.WindowState = window.WindowState == WindowState.Minimized
            ? WindowState.Normal.ToString()
            : window.WindowState.ToString();
        _settingsService.Save();
    }

    private static void EnsureOnScreen(Window window)
    {
        var workArea = SystemParameters.WorkArea;

        if (window.Width > workArea.Width)
            window.Width = workArea.Width;
        if (window.Height > workArea.Height)
            window.Height = workArea.Height;

        if (window.Left < workArea.Left)
            window.Left = workArea.Left;
        if (window.Top < workArea.Top)
            window.Top = workArea.Top;

        if (window.Left + window.Width > workArea.Right)
            window.Left = workArea.Right - window.Width;
        if (window.Top + window.Height > workArea.Bottom)
            window.Top = workArea.Bottom - window.Height;
    }
}
