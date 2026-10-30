using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quorum.App.Infrastructure.Auth;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Shared;

[Authorize]
public abstract class SignedInPageModel : PageModel
{
    protected AccountId AccountId =>
        SignInSession.Account(User) ?? throw new InvalidOperationException("A signed-in account is required.");

    protected GroupId? OpenGroupId => SignInSession.OpenGroup(User);

    protected async Task<IActionResult?> RequireGroupAsync(QuorumDbContext db, CancellationToken cancellationToken = default)
    {
        if (SignInSession.Account(User) is not { } accountId)
            return Challenge();

        if (OpenGroupId is not { } groupId)
        {
            var count = await db.Memberships.CountAsync(
                member => member.AccountId == accountId && member.RemovedAt == null,
                cancellationToken);
            return Redirect(count == 0 ? "/groups/new" : "/groups");
        }

        var belongs = await db.Memberships.AnyAsync(
            member => member.AccountId == accountId && member.GroupId == groupId && member.RemovedAt == null,
            cancellationToken);
        return belongs ? null : NotFound();
    }
}
