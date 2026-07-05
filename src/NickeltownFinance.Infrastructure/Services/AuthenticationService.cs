using LiteDB;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Core.Security;

namespace NickeltownFinance.Infrastructure.Services;

public class AuthenticationService : IAuthenticationService
{
    public const int MaxFailedLogins = 5;

    private readonly IUserRepository _userRepository;
    private readonly ISessionService _sessionService;
    private readonly ISettingsService _settingsService;
    private readonly IAuditService _auditService;

    public AuthenticationService(
        IUserRepository userRepository,
        ISessionService sessionService,
        ISettingsService settingsService,
        IAuditService auditService)
    {
        _userRepository = userRepository;
        _sessionService = sessionService;
        _settingsService = settingsService;
        _auditService = auditService;
    }

    public Task<(bool Success, string? Error)> LoginAsync(string username, string password, bool rememberUsername)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return Task.FromResult<(bool, string?)>((false, "Username and password are required."));

        var user = _userRepository.GetByUsername(username.Trim());
        if (user is null || !user.IsActive)
            return Task.FromResult<(bool, string?)>((false, "Invalid username or password."));

        if (user.IsLocked)
            return Task.FromResult<(bool, string?)>((false, "This account is locked. Contact an administrator to unlock it."));

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedLogins)
            {
                user.IsLocked = true;
                user.ModifiedDate = DateTime.UtcNow;
                _userRepository.Update(user);
                return Task.FromResult<(bool, string?)>((false, "Account locked after too many failed login attempts."));
            }

            user.ModifiedDate = DateTime.UtcNow;
            _userRepository.Update(user);
            return Task.FromResult<(bool, string?)>((false, "Invalid username or password."));
        }

        user.FailedLoginAttempts = 0;
        user.IsLocked = false;
        user.LastLoginUtc = DateTime.UtcNow;
        user.ModifiedDate = DateTime.UtcNow;
        _userRepository.Update(user);

        _sessionService.SetUser(user);

        _settingsService.RememberUsername = rememberUsername;
        _settingsService.LastUsername = rememberUsername ? user.Username : string.Empty;
        _settingsService.Save();

        return Task.FromResult<(bool, string?)>((true, null));
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(
        ObjectId userId, string currentPassword, string newPassword)
    {
        var user = _userRepository.GetById(userId);
        if (user is null)
            return (false, "User was not found.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return (false, "Current password is incorrect.");

        var passwordError = PasswordRules.Validate(newPassword);
        if (passwordError is not null)
            return (false, passwordError);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordChangedUtc = DateTime.UtcNow;
        user.ModifiedDate = DateTime.UtcNow;
        _userRepository.Update(user);

        await _auditService.LogAsync(AuditAction.PasswordChanged, user.Id, user.Username, "Changed by user");
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AdminResetPasswordAsync(
        string adminUsername,
        string adminPassword,
        string targetUsername,
        string newPassword)
    {
        var admin = _userRepository.GetByUsername(adminUsername.Trim());
        if (admin is null || admin.Role != UserRole.Administrator || !admin.IsActive || admin.IsLocked)
            return (false, "Administrator credentials are required.");

        if (!BCrypt.Net.BCrypt.Verify(adminPassword, admin.PasswordHash))
            return (false, "Administrator credentials are incorrect.");

        var target = _userRepository.GetByUsername(targetUsername.Trim());
        if (target is null)
            return (false, "Target user was not found.");

        var passwordError = PasswordRules.Validate(newPassword);
        if (passwordError is not null)
            return (false, passwordError);

        ApplyPasswordReset(target, newPassword);
        await _auditService.LogAsync(AuditAction.PasswordChanged, target.Id, target.Username,
            $"Reset by administrator '{admin.Username}'");

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ResetUserPasswordAsync(
        ObjectId targetUserId, string adminPassword, string newPassword)
    {
        var admin = _sessionService.CurrentUser;
        if (admin is null || admin.Role != UserRole.Administrator || !admin.IsActive)
            return (false, "Only administrators can reset passwords.");

        var freshAdmin = _userRepository.GetById(admin.Id);
        if (freshAdmin is null || !BCrypt.Net.BCrypt.Verify(adminPassword, freshAdmin.PasswordHash))
            return (false, "Your password is incorrect.");

        var target = _userRepository.GetById(targetUserId);
        if (target is null)
            return (false, "Target user was not found.");

        var passwordError = PasswordRules.Validate(newPassword);
        if (passwordError is not null)
            return (false, passwordError);

        ApplyPasswordReset(target, newPassword);
        await _auditService.LogAsync(AuditAction.PasswordChanged, target.Id, target.Username,
            "Reset by administrator");

        return (true, null);
    }

    public bool VerifyCurrentUserPassword(string password)
    {
        var user = _sessionService.CurrentUser;
        if (user is null || string.IsNullOrEmpty(password))
            return false;

        var fresh = _userRepository.GetById(user.Id);
        return fresh is not null && BCrypt.Net.BCrypt.Verify(password, fresh.PasswordHash);
    }

    private void ApplyPasswordReset(User target, string newPassword)
    {
        target.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        target.PasswordChangedUtc = DateTime.UtcNow;
        target.FailedLoginAttempts = 0;
        target.IsLocked = false;
        target.ModifiedDate = DateTime.UtcNow;
        _userRepository.Update(target);
    }
}
