using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using Wpf.Ui.Appearance;

namespace NickeltownFinance.Services;

public interface IThemeService
{
    bool IsDark { get; }

    event Action? ThemeChanged;

    void ApplyTheme(AppTheme theme);
}

public class ThemeService : IThemeService
{
    private readonly ISettingsService _settingsService;

    public ThemeService(ISettingsService settingsService) => _settingsService = settingsService;

    public bool IsDark { get; private set; } = true;

    public event Action? ThemeChanged;

    public void ApplyTheme(AppTheme theme)
    {
        var isDark = ResolveIsDark(theme);
        IsDark = isDark;

        ApplicationThemeManager.Apply(isDark ? ApplicationTheme.Dark : ApplicationTheme.Light);
        ApplyBrandPalette(isDark);
        _settingsService.Theme = theme;
        RefreshOpenWindows();
        ThemeChanged?.Invoke();
    }


    private static bool ResolveIsDark(AppTheme theme) =>
        theme switch
        {
            AppTheme.Light => false,
            AppTheme.Dark => true,
            _ => ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark
        };

    private static void ApplyBrandPalette(bool isDark)
    {
        var p = isDark ? Palette.Dark : Palette.Light;

        SetColor("BrandBackgroundColor", p.Background);
        SetColor("BrandSurfaceColor", p.Surface);
        SetColor("BrandSidebarColor", p.Sidebar);
        SetColor("BrandElevatedColor", p.Elevated);
        SetColor("BrandBorderColor", p.Border);
        SetColor("BrandBorderSubtleColor", p.BorderSubtle);
        SetColor("BrandPrimaryColor", p.Primary);
        SetColor("BrandPrimaryHoverColor", p.PrimaryHover);
        SetColor("BrandPrimaryPressedColor", p.PrimaryPressed);
        SetColor("BrandSuccessColor", p.Success);
        SetColor("BrandWarningColor", p.Warning);
        SetColor("BrandDangerColor", p.Danger);
        SetColor("BrandIncomeColor", p.Income);
        SetColor("BrandExpenseColor", p.Expense);
        SetColor("BrandProfitColor", p.Profit);
        SetColor("BrandBalanceColor", p.Balance);
        SetColor("BrandTextPrimaryColor", p.TextPrimary);
        SetColor("BrandTextSecondaryColor", p.TextSecondary);
        SetColor("BrandMutedTextColor", p.MutedText);
        SetColor("BrandPrimaryMidColor", p.Surface);
        SetColor("BrandAccentColor", p.Primary);
        SetColor("BrandAccentSoftColor", p.AccentSoft);
        SetColor("BrandCardColor", p.Surface);

        SetBrush("BrandBackgroundBrush", p.Background);
        SetBrush("BrandSurfaceBrush", p.Surface);
        SetBrush("BrandSidebarBrush", p.Sidebar);
        SetBrush("BrandElevatedBrush", p.Elevated);
        SetBrush("BrandBorderBrush", p.Border);
        SetBrush("BrandBorderSubtleBrush", p.BorderSubtle);
        SetBrush("BrandPrimaryBrush", p.Primary);
        SetBrush("BrandPrimaryHoverBrush", p.PrimaryHover);
        SetBrush("BrandPrimaryPressedBrush", p.PrimaryPressed);
        SetBrush("BrandPrimaryMidBrush", p.Surface);
        SetBrush("BrandAccentBrush", p.Primary);
        SetBrush("BrandAccentSoftBrush", p.AccentSoft);
        SetBrush("BrandSuccessBrush", p.Success);
        SetBrush("BrandWarningBrush", p.Warning);
        SetBrush("BrandDangerBrush", p.Danger);
        SetBrush("BrandIncomeBrush", p.Income);
        SetBrush("BrandExpenseBrush", p.Expense);
        SetBrush("BrandProfitBrush", p.Profit);
        SetBrush("BrandBalanceBrush", p.Balance);
        SetBrush("BrandTextPrimaryBrush", p.TextPrimary);
        SetBrush("BrandTextSecondaryBrush", p.TextSecondary);
        SetBrush("BrandMutedTextBrush", p.MutedText);
        SetBrush("BrandCardBrush", p.Surface);
        SetBrush("BrandErrorBrush", p.Danger);

        SetBrush("BrandAccentFillBrush", p.AccentFill);
        SetBrush("BrandNavActiveBrush", p.AccentFill);
        SetBrush("BrandNavHoverBrush", p.NavHover);
        SetBrush("BrandInsetBrush", p.Inset);
        SetBrush("BrandIncomeFillBrush", p.IncomeFill);
        SetBrush("BrandExpenseFillBrush", p.ExpenseFill);
        SetBrush("BrandProfitFillBrush", p.AccentFill);
        SetBrush("BrandBalanceFillBrush", p.AccentFill);
        SetBrush("BrandSuccessFillBrush", p.IncomeFill);
        SetBrush("BrandWarningFillBrush", p.WarningFill);
        SetBrush("BrandDangerFillBrush", p.ExpenseFill);
        SetBrush("BrandRowAltBrush", p.RowAlt);
        SetBrush("BrandOverlayBrush", p.Overlay);
        SetBrush("BrandGlassBrush", p.Glass);

        // WPF-UI chrome overrides (keep inputs/cards on-brand in both themes)
        SetBrush("ApplicationBackgroundBrush", p.Background);
        SetBrush("ControlFillColorDefaultBrush", p.Surface);
        SetBrush("ControlFillColorSecondaryBrush", p.Elevated);
        SetBrush("ControlFillColorTertiaryBrush", p.Inset);
        SetBrush("ControlFillColorInputActiveBrush", p.Elevated);
        SetBrush("ControlFillColorDisabledBrush", p.Inset);
        SetBrush("CardBackgroundFillColorDefaultBrush", p.Surface);
        SetBrush("CardBackgroundFillColorSecondaryBrush", p.Elevated);
        SetBrush("TextFillColorPrimaryBrush", p.TextPrimary);
        SetBrush("TextFillColorSecondaryBrush", p.TextSecondary);
        SetBrush("TextFillColorTertiaryBrush", p.MutedText);
        SetBrush("TextFillColorDisabledBrush", p.MutedText);
        SetBrush("TextFillColorPlaceholderBrush", p.MutedText);
        SetBrush("ControlStrokeColorDefaultBrush", p.Border);
        SetBrush("ControlStrokeColorSecondaryBrush", p.BorderSubtle);
        SetBrush("ControlSolidFillColorDefaultBrush", p.Surface);
        SetBrush("SubtleFillColorSecondaryBrush", p.Inset);
        SetBrush("SubtleFillColorTertiaryBrush", p.NavHover);
        SetBrush("SystemAccentColorPrimaryBrush", p.Primary);
        SetBrush("SystemAccentColorSecondaryBrush", p.PrimaryHover);
        SetBrush("SystemAccentColorTertiaryBrush", p.PrimaryPressed);
        SetBrush("ComboBoxBackground", p.Surface);
        SetBrush("ComboBoxBackgroundPointerOver", p.Elevated);
        SetBrush("ComboBoxBackgroundPressed", p.Elevated);
        SetBrush("ComboBoxBackgroundDisabled", p.Inset);
        SetBrush("TextControlBackground", p.Surface);
        SetBrush("TextControlBackgroundPointerOver", p.Elevated);
        SetBrush("TextControlBackgroundFocused", p.Elevated);
        SetBrush("TextControlForeground", p.TextPrimary);
        SetBrush("TextControlPlaceholderForeground", p.MutedText);
        SetBrush("TextControlBorderBrush", p.Border);

        // Classic WPF controls (ComboBox popup, DatePicker, etc.)
        SetSystemBrush(SystemColors.WindowBrushKey, p.Surface);
        SetSystemBrush(SystemColors.WindowTextBrushKey, p.TextPrimary);
        SetSystemBrush(SystemColors.ControlBrushKey, p.Surface);
        SetSystemBrush(SystemColors.ControlTextBrushKey, p.TextPrimary);
        SetSystemBrush(SystemColors.ControlLightBrushKey, p.Elevated);
        SetSystemBrush(SystemColors.ControlLightLightBrushKey, p.Elevated);
        SetSystemBrush(SystemColors.ControlDarkBrushKey, p.Border);
        SetSystemBrush(SystemColors.ControlDarkDarkBrushKey, p.BorderSubtle);
        SetSystemBrush(SystemColors.GrayTextBrushKey, p.MutedText);
        SetSystemBrush(SystemColors.HighlightBrushKey, p.Primary);
        SetSystemBrush(SystemColors.HighlightTextBrushKey, Colors.White);
        SetSystemBrush(SystemColors.InactiveSelectionHighlightBrushKey, p.AccentFill);
        SetSystemBrush(SystemColors.MenuBrushKey, p.Elevated);
        SetSystemBrush(SystemColors.MenuTextBrushKey, p.TextPrimary);

        // Design-system aliases
        SetBrush("DsIncomeBrush", p.Income);
        SetBrush("DsExpenseBrush", p.Expense);
        SetBrush("DsProfitBrush", p.Profit);
        SetBrush("DsErrorBrush", p.Danger);
        SetBrush("DsSuccessBrush", p.Success);
        SetBrush("DsWarningBrush", p.Warning);

        SetShadowOpacity("CardShadow", p.ShadowOpacity);
        SetShadowOpacity("CardShadowSoft", p.ShadowOpacity * 0.65);
        SetShadowOpacity("ElevatedShadow", p.ShadowOpacity * 1.15);
        SetShadowOpacity("QuietSurfaceShadow", p.ShadowOpacity * 0.75);
    }

    private static void SetColor(string key, Color color)
    {
        if (Application.Current is null) return;
        Application.Current.Resources[key] = color;
    }

    private static void SetBrush(string key, Color color)
    {
        if (Application.Current is null) return;

        // Mutate the live brush instance (StaticResource keeps working) and
        // publish it at the application level (DynamicResource updates live).
        if (Application.Current.TryFindResource(key) is SolidColorBrush existing)
        {
            if (existing.IsFrozen)
            {
                var clone = existing.Clone();
                clone.Color = color;
                Application.Current.Resources[key] = clone;
            }
            else
            {
                existing.Color = color;
                Application.Current.Resources[key] = existing;
            }

            return;
        }

        Application.Current.Resources[key] = new SolidColorBrush(color);
    }

    private static void SetSystemBrush(ResourceKey key, Color color)
    {
        if (Application.Current is null) return;
        Application.Current.Resources[key] = new SolidColorBrush(color);
    }

    private static void RefreshOpenWindows()
    {
        if (Application.Current is null) return;

        var background = Application.Current.TryFindResource("BrandBackgroundBrush") as Brush;
        foreach (Window window in Application.Current.Windows)
        {
            if (background is not null)
                window.Background = background;

            window.InvalidateVisual();
            if (window.Content is FrameworkElement root)
                root.InvalidateVisual();
        }
    }

    private static void SetShadowOpacity(string key, double opacity)
    {
        if (Application.Current?.TryFindResource(key) is not DropShadowEffect effect)
            return;

        if (effect.IsFrozen)
        {
            var clone = effect.Clone();
            clone.Opacity = opacity;
            Application.Current.Resources[key] = clone;
            return;
        }

        effect.Opacity = opacity;
    }

    private sealed class Palette
    {
        public required Color Background { get; init; }
        public required Color Surface { get; init; }
        public required Color Sidebar { get; init; }
        public required Color Elevated { get; init; }
        public required Color Border { get; init; }
        public required Color BorderSubtle { get; init; }
        public required Color Primary { get; init; }
        public required Color PrimaryHover { get; init; }
        public required Color PrimaryPressed { get; init; }
        public required Color Success { get; init; }
        public required Color Warning { get; init; }
        public required Color Danger { get; init; }
        public required Color Income { get; init; }
        public required Color Expense { get; init; }
        public required Color Profit { get; init; }
        public required Color Balance { get; init; }
        public required Color TextPrimary { get; init; }
        public required Color TextSecondary { get; init; }
        public required Color MutedText { get; init; }
        public required Color AccentSoft { get; init; }
        public required Color AccentFill { get; init; }
        public required Color NavHover { get; init; }
        public required Color Inset { get; init; }
        public required Color IncomeFill { get; init; }
        public required Color ExpenseFill { get; init; }
        public required Color WarningFill { get; init; }
        public required Color RowAlt { get; init; }
        public required Color Overlay { get; init; }
        public required Color Glass { get; init; }
        public required double ShadowOpacity { get; init; }

        public static readonly Palette Dark = new()
        {
            Background = Rgb(0x0C, 0x0E, 0x13),
            Surface = Rgb(0x15, 0x18, 0x20),
            Sidebar = Rgb(0x10, 0x13, 0x1A),
            Elevated = Rgb(0x1C, 0x21, 0x2C),
            Border = Rgb(0x2A, 0x30, 0x40),
            BorderSubtle = Rgb(0x1E, 0x24, 0x30),
            Primary = Rgb(0x4B, 0x9E, 0xFF),
            PrimaryHover = Rgb(0x6B, 0xB0, 0xFF),
            PrimaryPressed = Rgb(0x2B, 0x7A, 0xE8),
            Success = Rgb(0x22, 0xC5, 0x5E),
            Warning = Rgb(0xF5, 0x9E, 0x0B),
            Danger = Rgb(0xEF, 0x44, 0x44),
            Income = Rgb(0x22, 0xC5, 0x5E),
            Expense = Rgb(0xEF, 0x44, 0x44),
            Profit = Rgb(0x4B, 0x9E, 0xFF),
            Balance = Rgb(0x4B, 0x9E, 0xFF),
            TextPrimary = Rgb(0xFF, 0xFF, 0xFF),
            TextSecondary = Rgb(0xA8, 0xB2, 0xC0),
            MutedText = Rgb(0x6B, 0x77, 0x88),
            AccentSoft = Rgb(0x8A, 0xC4, 0xFF),
            AccentFill = Argb(0x38, 0x4B, 0x9E, 0xFF),
            NavHover = Argb(0x12, 0xFF, 0xFF, 0xFF),
            Inset = Argb(0x0A, 0xFF, 0xFF, 0xFF),
            IncomeFill = Argb(0x29, 0x22, 0xC5, 0x5E),
            ExpenseFill = Argb(0x29, 0xEF, 0x44, 0x44),
            WarningFill = Argb(0x29, 0xF5, 0x9E, 0x0B),
            RowAlt = Argb(0x08, 0xFF, 0xFF, 0xFF),
            Overlay = Argb(0xD9, 0x0C, 0x0E, 0x13),
            Glass = Argb(0xCC, 0x10, 0x13, 0x1A),
            ShadowOpacity = 0.35
        };

        public static readonly Palette Light = new()
        {
            Background = Rgb(0xF0, 0xF2, 0xF7),
            Surface = Rgb(0xFF, 0xFF, 0xFF),
            Sidebar = Rgb(0xF5, 0xF7, 0xFB),
            Elevated = Rgb(0xFA, 0xFB, 0xFD),
            Border = Rgb(0xD8, 0xDE, 0xE8),
            BorderSubtle = Rgb(0xE8, 0xEC, 0xF2),
            Primary = Rgb(0x2B, 0x7A, 0xE8),
            PrimaryHover = Rgb(0x4B, 0x9E, 0xFF),
            PrimaryPressed = Rgb(0x1A, 0x64, 0xC8),
            Success = Rgb(0x16, 0xA3, 0x4A),
            Warning = Rgb(0xD9, 0x77, 0x06),
            Danger = Rgb(0xDC, 0x26, 0x26),
            Income = Rgb(0x16, 0xA3, 0x4A),
            Expense = Rgb(0xDC, 0x26, 0x26),
            Profit = Rgb(0x2B, 0x7A, 0xE8),
            Balance = Rgb(0x2B, 0x7A, 0xE8),
            TextPrimary = Rgb(0x0F, 0x12, 0x18),
            TextSecondary = Rgb(0x4A, 0x54, 0x64),
            MutedText = Rgb(0x6B, 0x76, 0x86),
            AccentSoft = Rgb(0x4B, 0x9E, 0xFF),
            AccentFill = Argb(0x24, 0x2B, 0x7A, 0xE8),
            NavHover = Argb(0x0C, 0x00, 0x00, 0x00),
            Inset = Argb(0x08, 0x00, 0x00, 0x00),
            IncomeFill = Argb(0x1F, 0x16, 0xA3, 0x4A),
            ExpenseFill = Argb(0x1F, 0xDC, 0x26, 0x26),
            WarningFill = Argb(0x1F, 0xD9, 0x77, 0x06),
            RowAlt = Argb(0x06, 0x00, 0x00, 0x00),
            Overlay = Argb(0x99, 0xF0, 0xF2, 0xF7),
            Glass = Argb(0xCC, 0xF5, 0xF7, 0xFB),
            ShadowOpacity = 0.14
        };

        private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
        private static Color Argb(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);
    }
}
