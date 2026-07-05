namespace NickeltownFinance.Core.Security;

public static class PasswordRules
{
    public const int MinimumLength = 8;

    public static bool HasUppercase(string password) => password.Any(char.IsUpper);

    public static bool HasLowercase(string password) => password.Any(char.IsLower);

    public static bool HasDigit(string password) => password.Any(char.IsDigit);

    public static bool HasSpecial(string password) =>
        password.Any(c => !char.IsLetterOrDigit(c));

    public static bool MeetsRequirements(string password) =>
        !string.IsNullOrEmpty(password)
        && password.Length >= MinimumLength
        && HasUppercase(password)
        && HasLowercase(password)
        && HasDigit(password)
        && HasSpecial(password);

    public static string? Validate(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
            return $"Password must be at least {MinimumLength} characters.";

        if (!HasUppercase(password))
            return "Password must include an uppercase letter.";

        if (!HasLowercase(password))
            return "Password must include a lowercase letter.";

        if (!HasDigit(password))
            return "Password must include a number.";

        if (!HasSpecial(password))
            return "Password must include a special character.";

        return null;
    }

    /// <summary>Returns 0–4 strength score.</summary>
    public static int GetStrength(string password)
    {
        if (string.IsNullOrEmpty(password))
            return 0;

        var score = 0;
        if (password.Length >= MinimumLength) score++;
        if (HasUppercase(password) && HasLowercase(password)) score++;
        if (HasDigit(password)) score++;
        if (HasSpecial(password)) score++;
        if (password.Length >= 12) score = Math.Min(4, score + 1);
        return Math.Min(4, score);
    }

    public static string GetStrengthLabel(int strength) => strength switch
    {
        0 => "Very weak",
        1 => "Weak",
        2 => "Fair",
        3 => "Good",
        _ => "Strong"
    };
}
