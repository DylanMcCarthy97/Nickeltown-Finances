namespace NickeltownFinance.Core.Enums;

public enum UserRole
{
    Administrator,
    Treasurer,
    Committee,
    ReadOnly
}

public static class UserRoleExtensions
{
    public static string ToDisplayName(this UserRole role) => role switch
    {
        UserRole.Administrator => "Administrator",
        UserRole.Treasurer => "Treasurer",
        UserRole.Committee => "Committee",
        UserRole.ReadOnly => "Read Only",
        _ => role.ToString()
    };
}
