using System.Net.Mail;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Accounts;

public sealed class Account
{
    public const int PasswordMin = 10;
    public const int PasswordMax = 128;

    public AccountId Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }

    public static bool PasswordAccepted(string? password) =>
        password is not null && password.Length >= PasswordMin && password.Length <= PasswordMax;

    public static bool EmailAccepted(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        var trimmed = email.Trim();
        if (trimmed.Length > 320 || trimmed.Contains(' ') || !trimmed.Contains('.'))
            return false;
        try
        {
            var parsed = new MailAddress(trimmed);
            return parsed.Address.Equals(trimmed, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
