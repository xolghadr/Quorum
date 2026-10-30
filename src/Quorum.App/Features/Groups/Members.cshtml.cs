using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;

namespace Quorum.App.Features.Groups;

public sealed class MembersModel : SignedInPageModel
{
    private readonly QuorumDbContext _db;
    private readonly RosterCommands _roster;
    private readonly SafeLog _log;

    public MembersModel(QuorumDbContext db, RosterCommands roster, SafeLog log)
    {
        _db = db;
        _roster = roster;
        _log = log;
    }

    public bool IsCreator { get; private set; }
    public IReadOnlyList<MemberRow> Members { get; private set; } = [];
    public string? Error { get; set; }

    [BindProperty]
    public string DisplayName { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var gate = await RequireGroupAsync(_db, cancellationToken);
        if (gate is not null)
            return gate;
        await LoadAsync(cancellationToken);
        if (Request.Query.ContainsKey("error"))
        {
            _log.AdoptReference(Request.Query["reference"]);
            Error = RosterCommands.InvalidNameMessage;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var gate = await RequireGroupAsync(_db, cancellationToken);
        if (gate is not null)
            return gate;

        var before = await _db.Memberships.CountAsync(
            member => member.GroupId == OpenGroupId && member.RemovedAt == null,
            cancellationToken);
        var result = await _roster.AddAsync(OpenGroupId!.Value, AccountId, DisplayName, cancellationToken);
        if (!result.Succeeded)
        {
            Error = before >= Group.MemberMax ? RosterCommands.GroupFullMessage : RosterCommands.InvalidNameMessage;
            await LoadAsync(cancellationToken);
            return Page();
        }

        return Redirect("/members");
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var group = await _db.Groups.FirstAsync(row => row.Id == OpenGroupId, cancellationToken);
        IsCreator = group.CreatorAccountId == AccountId;
        Members = await _db.Memberships
            .Where(member => member.GroupId == OpenGroupId && member.RemovedAt == null)
            .OrderBy(member => member.CreatedAt)
            .Select(member => new MemberRow(member.Id.Value, member.DisplayName, member.AccountId == group.CreatorAccountId))
            .ToListAsync(cancellationToken);
    }

    public sealed record MemberRow(Guid Id, string DisplayName, bool IsCreator);
}
