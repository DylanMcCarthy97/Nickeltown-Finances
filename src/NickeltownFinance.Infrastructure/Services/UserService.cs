using LiteDB;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Core.Security;

namespace NickeltownFinance.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionService _sessionService;
    private readonly IAuditService _auditService;

    public UserService(
        IUserRepository userRepository,
        ISessionService sessionService,
        IAuditService auditService)
    {
        _userRepository = userRepository;
        _sessionService = sessionService;
        _auditService = auditService;
    }

    public Task<IReadOnlyList<User>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<User>>(
            _userRepository.GetAll().OrderBy(u => u.DisplayName).ToList());

    public Task<User?> GetByIdAsync(ObjectId userId) =>
        Task.FromResult(_userRepository.GetById(userId));

    public bool IsUsernameAvailable(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        return _userRepository.GetByUsername(username.Trim().ToLowerInvariant()) is null;
    }

    public async Task<(bool Success, string? Error)> CreateAsync(
        string username,
        string displayName,
        string password,
        UserRole role,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(username))
            return (false, "Username is required.");

        if (string.IsNullOrWhiteSpace(displayName))
            return (false, "Display name is required.");

        var passwordError = PasswordRules.Validate(password);
        if (passwordError is not null)
            return (false, passwordError);

        var normalized = username.Trim().ToLowerInvariant();
        if (_userRepository.GetByUsername(normalized) is not null)
            return (false, "Username is already in use.");

        var user = new User
        {
            Username = normalized,
            DisplayName = displayName.Trim(),
            Role = role,
            IsActive = isActive,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            PasswordChangedUtc = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };

        _userRepository.Insert(user);
        await _auditService.LogAsync(AuditAction.UserCreated, user.Id, user.Username,
            $"Role: {role.ToDisplayName()}, Active: {isActive}");

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        ObjectId userId,
        string username,
        string displayName,
        UserRole role,
        bool isActive,
        string? email,
        string? profilePicturePath,
        string? signatureImagePath)
    {
        var user = _userRepository.GetById(userId);
        if (user is null)
            return (false, "User was not found.");

        if (string.IsNullOrWhiteSpace(username))
            return (false, "Username is required.");

        if (string.IsNullOrWhiteSpace(displayName))
            return (false, "Display name is required.");

        var currentUserId = _sessionService.CurrentUser?.Id;
        if (userId == currentUserId && !isActive)
            return (false, "You cannot deactivate your own account.");

        if (user.Role == UserRole.Administrator && (role != UserRole.Administrator || !isActive))
        {
            if (_userRepository.CountAdministrators(userId) == 0)
                return (false, "Cannot remove or deactivate the last administrator.");
        }

        var normalized = username.Trim().ToLowerInvariant();
        var existing = _userRepository.GetByUsername(normalized);
        if (existing is not null && existing.Id != userId)
            return (false, "Username is already in use.");

        var previousRole = user.Role;
        var previousActive = user.IsActive;

        user.Username = normalized;
        user.DisplayName = displayName.Trim();
        user.Role = role;
        user.IsActive = isActive;
        user.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        user.ProfilePicturePath = string.IsNullOrWhiteSpace(profilePicturePath) ? null : profilePicturePath;
        user.SignatureImagePath = string.IsNullOrWhiteSpace(signatureImagePath) ? null : signatureImagePath;
        user.ModifiedDate = DateTime.UtcNow;

        _userRepository.Update(user);
        RefreshSessionUser(user);

        if (previousRole != role)
        {
            await _auditService.LogAsync(AuditAction.RoleChanged, user.Id, user.Username,
                $"{previousRole.ToDisplayName()} → {role.ToDisplayName()}");
        }

        if (previousActive != isActive)
        {
            await _auditService.LogAsync(
                isActive ? AuditAction.UserActivated : AuditAction.UserDeactivated,
                user.Id, user.Username);
        }

        await _auditService.LogAsync(AuditAction.UserUpdated, user.Id, user.Username);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(ObjectId userId)
    {
        var user = _userRepository.GetById(userId);
        if (user is null)
            return (false, "User was not found.");

        if (userId == _sessionService.CurrentUser?.Id)
            return (false, "You cannot delete your own account while logged in.");

        if (user.Role == UserRole.Administrator && _userRepository.CountAdministrators(userId) == 0)
            return (false, "Cannot delete the last administrator.");

        var username = user.Username;
        _userRepository.Delete(userId);
        await _auditService.LogAsync(AuditAction.UserDeleted, userId, username);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SetActiveAsync(ObjectId userId, bool isActive)
    {
        var user = _userRepository.GetById(userId);
        if (user is null)
            return (false, "User was not found.");

        if (userId == _sessionService.CurrentUser?.Id && !isActive)
            return (false, "You cannot deactivate your own account.");

        if (!isActive && user.Role == UserRole.Administrator &&
            _userRepository.CountAdministrators(userId) == 0)
            return (false, "Cannot deactivate the last administrator.");

        user.IsActive = isActive;
        user.ModifiedDate = DateTime.UtcNow;
        _userRepository.Update(user);

        await _auditService.LogAsync(
            isActive ? AuditAction.UserActivated : AuditAction.UserDeactivated,
            user.Id, user.Username);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UnlockAsync(ObjectId userId)
    {
        var user = _userRepository.GetById(userId);
        if (user is null)
            return (false, "User was not found.");

        user.IsLocked = false;
        user.FailedLoginAttempts = 0;
        user.ModifiedDate = DateTime.UtcNow;
        _userRepository.Update(user);

        await _auditService.LogAsync(AuditAction.UserUnlocked, user.Id, user.Username);
        return (true, null);
    }

    public Task<(bool Success, string? Error)> UpdateSignatureAsync(ObjectId userId, string? signatureImagePath)
    {
        var user = _userRepository.GetById(userId);
        if (user is null)
            return Task.FromResult<(bool, string?)>((false, "User was not found."));

        var current = _sessionService.CurrentUser;
        if (current is null)
            return Task.FromResult<(bool, string?)>((false, "You must be signed in."));

        // Users may update their own signature; administrators may update anyone's.
        if (current.Id != userId && current.Role != UserRole.Administrator)
            return Task.FromResult<(bool, string?)>((false, "You can only update your own signature."));

        user.SignatureImagePath = string.IsNullOrWhiteSpace(signatureImagePath) ? null : signatureImagePath;
        user.ModifiedDate = DateTime.UtcNow;
        _userRepository.Update(user);
        RefreshSessionUser(user);

        return Task.FromResult<(bool, string?)>((true, null));
    }

    private void RefreshSessionUser(User user)
    {
        if (_sessionService.CurrentUser?.Id == user.Id)
            _sessionService.SetUser(user);
    }
}
