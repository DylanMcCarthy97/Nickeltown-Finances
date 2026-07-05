using CommunityToolkit.Mvvm.ComponentModel;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.ViewModels;

public partial class UserRowViewModel : ObservableObject
{
    public UserRowViewModel(User user) => Apply(user);

    public User Source { get; private set; } = null!;

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _roleDisplay = string.Empty;
    [ObservableProperty] private UserRole _role;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isLocked;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _statusKind = "Inactive";
    [ObservableProperty] private string _lastLoginDisplay = "Never";
    [ObservableProperty] private string _createdDisplay = string.Empty;
    [ObservableProperty] private string _passwordChangedDisplay = "Never";
    [ObservableProperty] private string _avatarInitials = "?";
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _profilePicturePath;
    [ObservableProperty] private string? _signatureImagePath;
    [ObservableProperty] private bool _hasSignature;

    public void Apply(User user)
    {
        Source = user;
        Username = user.Username;
        DisplayName = user.DisplayName;
        Role = user.Role;
        RoleDisplay = user.Role.ToDisplayName();
        IsActive = user.IsActive;
        IsLocked = user.IsLocked;
        Email = user.Email;
        ProfilePicturePath = user.ProfilePicturePath;
        SignatureImagePath = user.SignatureImagePath;
        HasSignature = !string.IsNullOrWhiteSpace(user.SignatureImagePath) &&
                       File.Exists(user.SignatureImagePath);

        if (user.IsLocked)
        {
            StatusText = "Locked";
            StatusKind = "Locked";
        }
        else if (user.IsActive)
        {
            StatusText = "Active";
            StatusKind = "Active";
        }
        else
        {
            StatusText = "Inactive";
            StatusKind = "Inactive";
        }

        LastLoginDisplay = user.LastLoginUtc is { } login
            ? login.ToLocalTime().ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)
            : "Never";

        CreatedDisplay = user.CreatedDate.ToLocalTime()
            .ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        PasswordChangedDisplay = user.PasswordChangedUtc is { } changed
            ? changed.ToLocalTime().ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)
            : "Never";

        AvatarInitials = BuildInitials(user.DisplayName, user.Username);
    }

    private static string BuildInitials(string displayName, string username)
    {
        var source = string.IsNullOrWhiteSpace(displayName) ? username : displayName;
        var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        if (parts.Length == 1 && parts[0].Length >= 2)
            return parts[0][..2].ToUpperInvariant();
        return string.IsNullOrEmpty(source) ? "?" : char.ToUpperInvariant(source[0]).ToString();
    }
}
