using Microsoft.AspNetCore.Mvc;
using Quorum.App.Features.Shared;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Groups;

public sealed class RenameMemberModel : SignedInPageModel
{
    private readonly RosterCommands _roster;

    public RenameMemberModel(RosterCommands roster)
    {
        _roster = roster;
    }

    public async Task<IActionResult> OnPostAsync(Guid memberId, string? displayName, CancellationToken cancellationToken)
    {
        if (OpenGroupId is not { } groupId)
            return Challenge();
        var result = await _roster.RenameAsync(groupId, AccountId, new MemberId(memberId), displayName, cancellationToken);
        if (result.Status == Shared.CommandStatus.NotFound)
            return NotFound();
        return Redirect(result.Succeeded ? "/members" : "/members?error=1");
    }
}

public sealed class RemoveMemberModel : SignedInPageModel
{
    private readonly RosterCommands _roster;

    public RemoveMemberModel(RosterCommands roster)
    {
        _roster = roster;
    }

    public async Task<IActionResult> OnPostAsync(Guid memberId, CancellationToken cancellationToken)
    {
        if (OpenGroupId is not { } groupId)
            return Challenge();
        var result = await _roster.RemoveAsync(groupId, AccountId, new MemberId(memberId), cancellationToken);
        if (result.Status == Shared.CommandStatus.NotFound)
            return NotFound();
        return Redirect(result.Succeeded ? "/members" : "/members?error=1");
    }
}
