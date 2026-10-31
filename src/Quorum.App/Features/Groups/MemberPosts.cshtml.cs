using Microsoft.AspNetCore.Mvc;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Auth;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Groups;

public sealed class RenameMemberModel : SignedInPageModel
{
    private readonly RosterCommands _roster;
    private readonly SafeLog _log;

    public RenameMemberModel(RosterCommands roster, SafeLog log)
    {
        _roster = roster;
        _log = log;
    }

    public async Task<IActionResult> OnPostAsync(Guid memberId, string? displayName, CancellationToken cancellationToken)
    {
        if (OpenGroupId is not { } groupId)
            return Challenge();
        var result = await _roster.RenameAsync(groupId, AccountId, new MemberId(memberId), displayName, cancellationToken);
        if (result.Status == Shared.CommandStatus.NotFound)
            return NotFound();
        return Redirect(result.Succeeded ? "/members" : "/members?error=1" + _log.ReferenceParameter());
    }
}

public sealed class RemoveMemberModel : SignedInPageModel
{
    private readonly RosterCommands _roster;
    private readonly SafeLog _log;

    public RemoveMemberModel(RosterCommands roster, SafeLog log)
    {
        _roster = roster;
        _log = log;
    }

    public async Task<IActionResult> OnPostAsync(Guid memberId, CancellationToken cancellationToken)
    {
        if (OpenGroupId is not { } groupId)
            return Challenge();
        var result = await _roster.RemoveAsync(groupId, AccountId, new MemberId(memberId), cancellationToken);
        if (result.Status == Shared.CommandStatus.NotFound)
            return NotFound();
        return Redirect(result.Succeeded ? "/members" : "/members?error=1" + _log.ReferenceParameter());
    }
}

public sealed class LeaveGroupModel : SignedInPageModel
{
    private readonly RosterCommands _roster;
    private readonly SafeLog _log;

    public LeaveGroupModel(RosterCommands roster, SafeLog log)
    {
        _roster = roster;
        _log = log;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (OpenGroupId is not { } groupId)
            return Challenge();
        var result = await _roster.LeaveAsync(groupId, AccountId, cancellationToken);
        if (result.Status == Shared.CommandStatus.NotFound)
            return NotFound();
        if (!result.Succeeded)
            return Redirect("/members?error=1" + _log.ReferenceParameter());

        await SignInSession.SignInAsync(HttpContext, AccountId, null);
        return Redirect("/groups");
    }
}
