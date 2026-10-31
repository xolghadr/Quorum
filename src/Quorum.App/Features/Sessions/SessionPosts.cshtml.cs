using Microsoft.AspNetCore.Mvc;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Sessions;

public sealed class AttendancePostModel : SignedInPageModel
{
    private readonly Election _election;
    private readonly SafeLog _log;

    public AttendancePostModel(Election election, SafeLog log)
    {
        _election = election;
        _log = log;
    }

    public async Task<IActionResult> OnPostAsync(Guid sessionId, Guid memberId, bool present, CancellationToken cancellationToken)
    {
        if (OpenGroupId is not { } groupId)
            return Challenge();
        var result = await _election.SaveAttendanceAsync(
            groupId,
            AccountId,
            new SessionId(sessionId),
            new MemberId(memberId),
            present,
            cancellationToken);
        if (result.Status == Shared.CommandStatus.NotFound)
            return NotFound();
        return Redirect(result.Succeeded ? $"/sessions/{sessionId}" : $"/sessions/{sessionId}?error=1" + _log.ReferenceParameter());
    }
}

public sealed class TopicsPostModel : SignedInPageModel
{
    private readonly SessionCommands _sessions;
    private readonly SafeLog _log;

    public TopicsPostModel(SessionCommands sessions, SafeLog log)
    {
        _sessions = sessions;
        _log = log;
    }

    public async Task<IActionResult> OnPostAsync(
        Guid sessionId,
        string? topic1,
        string? topic2,
        string? topic3,
        CancellationToken cancellationToken)
    {
        if (OpenGroupId is not { } groupId)
            return Challenge();
        var ids = new List<Guid>();
        foreach (var value in new[] { topic1, topic2, topic3 })
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;
            ids.Add(Guid.TryParse(value, out var id) ? id : Guid.Empty);
        }

        var result = await _sessions.SaveTopicsAsync(groupId, AccountId, new SessionId(sessionId), ids, cancellationToken);
        if (result.Status == Shared.CommandStatus.NotFound)
            return NotFound();
        return Redirect(result.Succeeded ? $"/sessions/{sessionId}" : $"/sessions/{sessionId}?error=1" + _log.ReferenceParameter());
    }
}

public sealed class VotesPostModel : SignedInPageModel
{
    private readonly Election _election;
    private readonly SafeLog _log;

    public VotesPostModel(Election election, SafeLog log)
    {
        _election = election;
        _log = log;
    }

    public async Task<IActionResult> OnPostAsync(
        Guid sessionId,
        Guid voterId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        if (OpenGroupId is not { } groupId)
            return Challenge();
        var result = await _election.CastVoteAsync(
            groupId,
            AccountId,
            new SessionId(sessionId),
            new MemberId(voterId),
            new MemberId(candidateId),
            cancellationToken);
        if (result.Status == Shared.CommandStatus.NotFound)
            return NotFound();
        return Redirect(result.Succeeded ? $"/sessions/{sessionId}" : $"/sessions/{sessionId}?error=1" + _log.ReferenceParameter());
    }
}
