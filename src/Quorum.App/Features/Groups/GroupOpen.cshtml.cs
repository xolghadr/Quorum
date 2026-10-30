using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Auth;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Groups;

public sealed class GroupOpenModel : SignedInPageModel
{
    private readonly QuorumDbContext _db;

    public GroupOpenModel(QuorumDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> OnPostAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var id = new GroupId(groupId);
        var belongs = await _db.Memberships.AnyAsync(
            member => member.AccountId == AccountId && member.GroupId == id && member.RemovedAt == null,
            cancellationToken);
        if (!belongs)
            return NotFound();

        await SignInSession.SignInAsync(HttpContext, AccountId, id);
        return Redirect("/catalog");
    }
}
