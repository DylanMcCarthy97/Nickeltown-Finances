using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Services;

/// <summary>
/// Resolves the Nickeltown Flounderers club logo for UI and reports.
/// </summary>
public static class ClubBranding
{
    public const string PackUri = "pack://application:,,,/Assets/ClubLogo.png";

    public static string DefaultLogoFilePath =>
        Path.Combine(AppPaths.FilesRoot, "club-logo.png");

    /// <summary>
    /// Ensures the bundled club logo exists on disk (for PDF/Excel reports).
    /// </summary>
    public static string EnsureDefaultLogoFile()
    {
        var dest = DefaultLogoFilePath;
        Directory.CreateDirectory(AppPaths.FilesRoot);

        if (!File.Exists(dest) || new FileInfo(dest).Length == 0)
        {
            var streamInfo = Application.GetResourceStream(new Uri(PackUri));
            if (streamInfo is null)
                throw new InvalidOperationException("Bundled club logo resource is missing.");

            using var stream = streamInfo.Stream;
            using var file = File.Create(dest);
            stream.CopyTo(file);
        }

        return dest;
    }

    /// <summary>
    /// Installs the default logo into settings when none is configured.
    /// </summary>
    public static void EnsureSettingsLogo(ISettingsService settings)
    {
        var changed = false;
        var path = settings.ClubLogoPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            settings.ClubLogoPath = EnsureDefaultLogoFile();
            changed = true;
        }

        // Align legacy default name with the official club logo wording.
        if (string.Equals(settings.ClubName, "Nickeltown Flounderers Car Club", StringComparison.OrdinalIgnoreCase))
        {
            settings.ClubName = "Nickeltown Flounderers Inc. Auto Club Kambalda";
            changed = true;
        }

        if (changed)
            settings.Save();
    }

    public static ImageSource LoadImage(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                // Load via memory so the file is not locked for later overwrite.
                var bytes = File.ReadAllBytes(path);
                using var stream = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                // Fall through to bundled resource.
            }
        }

        return (ImageSource)Application.Current.FindResource("DefaultClubLogoImage");
    }
}
