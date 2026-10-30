using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Accounts;

public enum AccountCommandStatus
{
    Created,
    SignedIn,
    Invalid,
    DuplicateEmail,
    Rejected,
}

public sealed record AccountCommandResult(AccountCommandStatus Status, Account? Account)
{
    public static AccountCommandResult Created(Account account) => new(AccountCommandStatus.Created, account);
    public static AccountCommandResult SignedIn(Account account) => new(AccountCommandStatus.SignedIn, account);
    public static AccountCommandResult Invalid() => new(AccountCommandStatus.Invalid, null);
    public static AccountCommandResult DuplicateEmail() => new(AccountCommandStatus.DuplicateEmail, null);
    public static AccountCommandResult Rejected() => new(AccountCommandStatus.Rejected, null);
}

public sealed class AccountCommands
{
    public const string DuplicateEmailMessage = "That email is already in use.";
    public const string InvalidRegistrationMessage = "Enter a valid email and a password of 10 to 128 characters.";
    public const string SignInFailureMessage = "The email or password is not valid.";

    private readonly QuorumDbContext _db;
    private readonly IPasswordHasher<Account> _hasher;
    private readonly IClock _clock;
    private readonly SafeLog _log;

    public AccountCommands(QuorumDbContext db, IPasswordHasher<Account> hasher, IClock clock, SafeLog log)
    {
        _db = db;
        _hasher = hasher;
        _clock = clock;
        _log = log;
    }

    public async Task<AccountCommandResult> RegisterAsync(string? email, string? password, CancellationToken cancellationToken = default)
    {
        if (!Account.EmailAccepted(email) || !Account.PasswordAccepted(password))
            return AccountCommandResult.Invalid();

        var normalized = Account.NormalizeEmail(email!);
        var taken = await _db.Accounts.AnyAsync(account => account.Email == normalized, cancellationToken);
        if (taken)
            return AccountCommandResult.DuplicateEmail();

        var account = new Account
        {
            Id = AccountId.New(),
            Email = normalized,
            CreatedAt = _clock.UtcNow,
        };
        account.PasswordHash = _hasher.HashPassword(account, password!);
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(cancellationToken);
        _log.Command("register", account.Id);
        return AccountCommandResult.Created(account);
    }

    public async Task<AccountCommandResult> SignInAsync(string? email, string? password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            return AccountCommandResult.Rejected();

        var normalized = Account.NormalizeEmail(email);
        var account = await _db.Accounts.FirstOrDefaultAsync(row => row.Email == normalized, cancellationToken);
        if (account is null)
            return AccountCommandResult.Rejected();

        var verification = _hasher.VerifyHashedPassword(account, account.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
            return AccountCommandResult.Rejected();

        _log.Command("sign-in", account.Id);
        return AccountCommandResult.SignedIn(account);
    }
}
