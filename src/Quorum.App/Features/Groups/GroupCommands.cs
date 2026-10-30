using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Groups;

public sealed class GroupCommands
{
    public const string InvalidGroupMessage =
        "Enter a group name of 1 to 60 characters and a display name of 1 to 40 characters.";

    private readonly QuorumDbContext _db;
    private readonly IClock _clock;
    private readonly SafeLog _log;

    public GroupCommands(QuorumDbContext db, IClock clock, SafeLog log)
    {
        _db = db;
        _clock = clock;
        _log = log;
    }

    public async Task<CommandOutcome> CreateAsync(
        AccountId accountId,
        string? name,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        _log.Begin();
        if (!Group.NameAccepted(name, out var groupName) || !Membership.DisplayNameAccepted(displayName, out var shown))
        {
            await RecordAsync(new ActivityWrite("create-group", false, accountId.Value), cancellationToken);
            return CommandOutcome.Refused();
        }

        var accountExists = await _db.Accounts.AnyAsync(account => account.Id == accountId, cancellationToken);
        if (!accountExists)
        {
            await RecordAsync(new ActivityWrite("create-group", false, accountId.Value), cancellationToken);
            return CommandOutcome.NotFound();
        }

        var group = new Group
        {
            Id = GroupId.New(),
            CreatorAccountId = accountId,
            Name = groupName,
            CreatedAt = _clock.UtcNow,
        };
        var membership = new Membership
        {
            Id = MemberId.New(),
            GroupId = group.Id,
            AccountId = accountId,
            DisplayName = shown,
            DisplayNameKey = Membership.KeyFor(shown),
            CreatedAt = _clock.UtcNow,
        };
        _db.Groups.Add(group);
        _db.Memberships.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAsync(new ActivityWrite("create-group", true, accountId.Value, group.Id.Value, MemberId: membership.Id.Value), cancellationToken);
        _log.Command("create-group", accountId, group.Id);
        return CommandOutcome.Ok(group.Id.Value);
    }

    private async Task RecordAsync(ActivityWrite write, CancellationToken cancellationToken)
    {
        await _log.RecordAsync(write, cancellationToken);
        _log.Finish();
    }
}
