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
        _log.Begin();
        if (!Account.EmailAccepted(email) || !Account.PasswordAccepted(password))
        {
            await RecordAsync(new ActivityWrite("register", false), cancellationToken);
            return AccountCommandResult.Invalid();
        }

        var normalized = Account.NormalizeEmail(email!);
        var taken = await _db.Accounts.AnyAsync(account => account.Email == normalized, cancellationToken);
        if (taken)
        {
            await RecordAsync(new ActivityWrite("register", false), cancellationToken);
            return AccountCommandResult.DuplicateEmail();
        }

        var account = new Account
        {
            Id = AccountId.New(),
            Email = normalized,
            CreatedAt = _clock.UtcNow,
        };
        account.PasswordHash = _hasher.HashPassword(account, password!);
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAsync(new ActivityWrite("register", true, account.Id.Value), cancellationToken);
        _log.Command("register", account.Id);
        return AccountCommandResult.Created(account);
    }

    public async Task<AccountCommandResult> SignInAsync(string? email, string? password, CancellationToken cancellationToken = default)
    {
        _log.Begin();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            await RecordAsync(new ActivityWrite("sign-in", false), cancellationToken);
            return AccountCommandResult.Rejected();
        }

        var normalized = Account.NormalizeEmail(email);
        var account = await _db.Accounts.FirstOrDefaultAsync(row => row.Email == normalized, cancellationToken);
        if (account is null)
        {
            await RecordAsync(new ActivityWrite("sign-in", false, Reason: ActivityRecord.UnknownAccount), cancellationToken);
            return AccountCommandResult.Rejected();
        }

        var verification = _hasher.VerifyHashedPassword(account, account.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            await RecordAsync(new ActivityWrite("sign-in", false, account.Id.Value), cancellationToken);
            return AccountCommandResult.Rejected();
        }

        await RecordAsync(new ActivityWrite("sign-in", true, account.Id.Value), cancellationToken);
        _log.Command("sign-in", account.Id);
        return AccountCommandResult.SignedIn(account);
    }

    private async Task RecordAsync(ActivityWrite write, CancellationToken cancellationToken)
    {
        await _log.RecordAsync(write, cancellationToken);
        _log.Finish();
    }
}
