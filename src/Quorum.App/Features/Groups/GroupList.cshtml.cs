using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Auth;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Groups;

public sealed class GroupListModel : SignedInPageModel
{
    private readonly QuorumDbContext _db;
    private readonly GroupCommands _groups;

    public GroupListModel(QuorumDbContext db, GroupCommands groups)
    {
        _db = db;
        _groups = groups;
    }

    public IReadOnlyList<GroupRow> Groups { get; private set; } = [];

    [BindProperty]
    public string Name { get; set; } = "";

    [BindProperty]
    public string DisplayName { get; set; } = "";

    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var memberships = await LoadAsync(cancellationToken);
        if (memberships.Count == 0)
            return Redirect("/groups/new");
        Groups = memberships;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await _groups.CreateAsync(AccountId, Name, DisplayName, cancellationToken);
        if (!result.Succeeded || result.Id is not { } id)
        {
            Error = GroupCommands.InvalidGroupMessage;
            Groups = await LoadAsync(cancellationToken);
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

    public sealed record GroupRow(Guid Id, string Name, string DisplayName);
}
