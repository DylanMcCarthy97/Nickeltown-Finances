using NickeltownFinance.Core.Enums;

namespace NickeltownFinance.Core.Models;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Reserved for future use.</summary>
    public string? Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsLocked { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTime? LastLoginUtc { get; set; }

    public DateTime? PasswordChangedUtc { get; set; }

    public string? ProfilePicturePath { get; set; }

    /// <summary>Digital signature image used on PDF reports printed by this user.</summary>
    public string? SignatureImagePath { get; set; }
}
