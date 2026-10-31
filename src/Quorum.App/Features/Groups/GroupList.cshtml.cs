using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Auth;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Groups;

public sealed class GroupListModel : SignedInPageModel
{
    private readonly QuorumDbContext _db;
    private readonly GroupCommands _groups;
    private readonly SafeLog _log;

    public GroupListModel(QuorumDbContext db, GroupCommands groups, SafeLog log)
    {
        _db = db;
        _groups = groups;
        _log = log;
    }

    public IReadOnlyList<GroupRow> Groups { get; private set; } = [];
    public IReadOnlyList<InvitationRow> Invitations { get; private set; } = [];

    [BindProperty]
    public string Name { get; set; } = "";

    [BindProperty]
    public string DisplayName { get; set; } = "";

    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Invitations = await LoadInvitationsAsync(cancellationToken);
        var memberships = await LoadAsync(cancellationToken);
        if (memberships.Count == 0 && Invitations.Count == 0)
            return Redirect("/groups/new");
        Groups = memberships;
        if (Request.Query.ContainsKey("error"))
        {
            _log.AdoptReference(Request.Query["reference"]);
            Error = InvitationCommands.NotAcceptedMessage;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await _groups.CreateAsync(AccountId, Name, DisplayName, cancellationToken);
        if (!result.Succeeded || result.Id is not { } id)
        {
            Error = GroupCommands.InvalidGroupMessage;
            Groups = await LoadAsync(cancellationToken);
            Invitations = await LoadInvitationsAsync(cancellationToken);
            return Page();
        }

        await SignInSession.SignInAsync(HttpContext, AccountId, new GroupId(id));
        return Redirect("/catalog");
    }

    private Task<List<GroupRow>> LoadAsync(CancellationToken cancellationToken) =>
        _db.Memberships
            .Where(member => member.AccountId == AccountId && member.RemovedAt == null)
            .Join(_db.Groups, member => member.GroupId, group => group.Id, (member, group) => new GroupRow(group.Id.Value, group.Name, member.DisplayName))
            .ToListAsync(cancellationToken);

    private async Task<List<InvitationRow>> LoadInvitationsAsync(CancellationToken cancellationToken)
    {
        var email = await _db.Accounts
            .Where(account => account.Id == AccountId)
            .Select(account => account.Email)
            .SingleAsync(cancellationToken);
        var pending = await (
            from invitation in _db.Invitations
            join groupRow in _db.Groups on invitation.GroupId equals groupRow.Id
            where invitation.EmailKey == email
                && invitation.AcceptedAt == null
                && invitation.DeclinedAt == null
                && invitation.CancelledAt == null
            orderby invitation.CreatedAt
            select new { invitation.Id, invitation.InvitedByAccountId, GroupId = groupRow.Id, groupRow.Name }).ToListAsync(cancellationToken);

        var rows = new List<InvitationRow>();
        foreach (var invitation in pending)
        {
            var inviter = await _db.Memberships
                .Where(member => member.GroupId == invitation.GroupId && member.AccountId == invitation.InvitedByAccountId)
                .OrderBy(member => member.RemovedAt == null ? 0 : 1)
                .Select(member => member.DisplayName)
                .FirstAsync(cancellationToken);
            rows.Add(new InvitationRow(invitation.Id.Value, invitation.Name, inviter));
        }

        return rows;
    }

    public sealed record GroupRow(Guid Id, string Name, string DisplayName);
    public sealed record InvitationRow(Guid Id, string GroupName, string InviterDisplayName);
}
