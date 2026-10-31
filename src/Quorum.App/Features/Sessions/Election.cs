using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Sessions;

public interface IFacilitatorTieBreak
{
    MemberId Choose(IReadOnlyList<MemberId> leaders);
}

public sealed class EqualChanceTieBreak : IFacilitatorTieBreak
{
    public MemberId Choose(IReadOnlyList<MemberId> leaders)
    {
        if (leaders.Count == 0)
            throw new InvalidOperationException("A tie needs at least one leader.");
        return leaders[Random.Shared.Next(leaders.Count)];
    }
}

public sealed class Election
{
    private readonly QuorumDbContext _db;
    private readonly IFacilitatorTieBreak _tieBreak;
    private readonly SafeLog _log;

    public Election(QuorumDbContext db, IFacilitatorTieBreak tieBreak, SafeLog log)
    {
        _db = db;
        _tieBreak = tieBreak;
        _log = log;
    }

    public async Task<CommandOutcome> SaveAttendanceAsync(
        GroupId groupId,
        AccountId accountId,
        SessionId sessionId,
        MemberId memberId,
        bool present,
        CancellationToken cancellationToken = default)
    {
        _log.Begin();
        var actor = await _db.Memberships.FirstOrDefaultAsync(
            member => member.GroupId == groupId && member.AccountId == accountId && member.RemovedAt == null,
            cancellationToken);
        if (actor is null)
            return await Refuse("mark-attendance", accountId, groupId, sessionId, memberId, CommandOutcome.NotFound(), cancellationToken);

        var session = await _db.Sessions.FirstOrDefaultAsync(
            row => row.Id == sessionId && row.GroupId == groupId,
            cancellationToken);
        if (session is null)
            return await Refuse("mark-attendance", accountId, groupId, sessionId, memberId, CommandOutcome.NotFound(), cancellationToken);
        if (actor.Id != memberId)
            return await Refuse("mark-attendance", accountId, groupId, sessionId, memberId, CommandOutcome.Refused(), cancellationToken);

        var existing = await _db.SessionAttendance.FirstOrDefaultAsync(
            row => row.SessionId == sessionId && row.MembershipId == memberId,
            cancellationToken);
        if (present && existing is null)
        {
            _db.SessionAttendance.Add(new SessionAttendance
            {
                SessionId = sessionId,
                MembershipId = memberId,
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        else if (!present && existing is not null)
        {
            _db.SessionAttendance.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }

        await RecordAsync(new ActivityWrite("mark-attendance", true, accountId.Value, groupId.Value, sessionId.Value, memberId.Value), cancellationToken);
        _log.Finish();
        _log.Command("mark-attendance", accountId, groupId, sessionId);
        return CommandOutcome.Ok();
    }

    public Task<CommandOutcome> CastVoteAsync(
        GroupId groupId,
        AccountId accountId,
        SessionId sessionId,
        MemberId voterId,
        MemberId candidateId,
        CancellationToken cancellationToken = default) =>
        CastVoteAsync(groupId, accountId, sessionId, voterId, candidateId, _tieBreak, cancellationToken);

    public async Task<CommandOutcome> CastVoteAsync(
        GroupId groupId,
        AccountId accountId,
        SessionId sessionId,
        MemberId voterId,
        MemberId candidateId,
        IFacilitatorTieBreak tieBreak,
        CancellationToken cancellationToken = default)
    {
        _log.Begin();
        var actor = await _db.Memberships.FirstOrDefaultAsync(
            member => member.GroupId == groupId && member.AccountId == accountId && member.RemovedAt == null,
            cancellationToken);
        if (actor is null)
            return await Refuse("cast-vote", accountId, groupId, sessionId, voterId, CommandOutcome.NotFound(), cancellationToken);
        if (actor.Id != voterId)
            return await Refuse("cast-vote", accountId, groupId, sessionId, voterId, CommandOutcome.Refused(), cancellationToken);

        var session = await _db.Sessions.FirstOrDefaultAsync(
            row => row.Id == sessionId && row.GroupId == groupId,
            cancellationToken);
        if (session is null)
            return await Refuse("cast-vote", accountId, groupId, sessionId, voterId, CommandOutcome.NotFound(), cancellationToken);
        if (session.FacilitatorMembershipId is not null)
            return await Refuse("cast-vote", accountId, groupId, sessionId, voterId, CommandOutcome.Refused(), cancellationToken);

        var voters = await _db.SessionVoters
            .Where(voter => voter.SessionId == sessionId)
            .Select(voter => voter.MembershipId)
            .ToListAsync(cancellationToken);
        IReadOnlyList<MemberId> votingSet;
        if (voters.Count == 0)
        {
            var attendance = await (
                from row in _db.SessionAttendance
                join member in _db.Memberships on row.MembershipId equals member.Id
                where row.SessionId == sessionId && member.AccountId != null && member.RemovedAt == null
                select row.MembershipId).ToListAsync(cancellationToken);
            if (attendance.Count is < 2 or > Group.MemberMax)
                return await Refuse("cast-vote", accountId, groupId, sessionId, voterId, CommandOutcome.Refused(), cancellationToken);
            if (!attendance.Contains(voterId) || !attendance.Contains(candidateId))
                return await Refuse("cast-vote", accountId, groupId, sessionId, voterId, CommandOutcome.Refused(), cancellationToken);
            if (attendance.Count >= 3 && voterId == candidateId)
                return await Refuse("cast-vote", accountId, groupId, sessionId, voterId, CommandOutcome.Refused(), cancellationToken);

            foreach (var memberId in attendance)
            {
                _db.SessionVoters.Add(new SessionVoter
                {
                    SessionId = sessionId,
                    MembershipId = memberId,
                });
            }

            votingSet = attendance;
        }
        else
        {
            if (!voters.Contains(voterId) || !voters.Contains(candidateId))
                return await Refuse("cast-vote", accountId, groupId, sessionId, voterId, CommandOutcome.Refused(), cancellationToken);
            if (voters.Count >= 3 && voterId == candidateId)
                return await Refuse("cast-vote", accountId, groupId, sessionId, voterId, CommandOutcome.Refused(), cancellationToken);
            var alreadyVoted = await _db.SessionVotes.AnyAsync(
                vote => vote.SessionId == sessionId && vote.VoterMembershipId == voterId,
                cancellationToken);
            if (alreadyVoted)
                return await Refuse("cast-vote", accountId, groupId, sessionId, voterId, CommandOutcome.Refused(), cancellationToken);
            votingSet = voters;
        }

        _db.SessionVotes.Add(new SessionVote
        {
            SessionId = sessionId,
            VoterMembershipId = voterId,
            CandidateMembershipId = candidateId,
        });

        var storedVotes = await _db.SessionVotes
            .Where(vote => vote.SessionId == sessionId)
            .Select(vote => new { vote.VoterMembershipId, vote.CandidateMembershipId })
            .ToListAsync(cancellationToken);
        var ballots = storedVotes
            .Select(vote => (vote.VoterMembershipId, vote.CandidateMembershipId))
            .Append((voterId, candidateId))
            .ToList();
        var facilitatorBefore = session.FacilitatorMembershipId;
        if (ballots.Select(ballot => ballot.Item1).Distinct().Count() == votingSet.Count)
            Elect(session, votingSet, ballots, tieBreak);

        await _db.SaveChangesAsync(cancellationToken);
        await RecordAsync(new ActivityWrite("cast-vote", true, accountId.Value, groupId.Value, sessionId.Value, voterId.Value), cancellationToken);
        if (facilitatorBefore is null && session.FacilitatorMembershipId is { } facilitatorId)
            await RecordAsync(new ActivityWrite("elect-facilitator", true, accountId.Value, groupId.Value, sessionId.Value, facilitatorId.Value), cancellationToken);
        _log.Finish();
        _log.Command("cast-vote", accountId, groupId, sessionId);
        return CommandOutcome.Ok();
    }

    public async Task ApplyDepartureAsync(GroupId groupId, MemberId memberId, CancellationToken cancellationToken = default)
    {
        var sessions = await _db.Sessions.Where(session => session.GroupId == groupId).ToListAsync(cancellationToken);
        if (sessions.Count == 0)
            return;

        var voters = await (
            from voter in _db.SessionVoters
            join session in _db.Sessions on voter.SessionId equals session.Id
            where session.GroupId == groupId
            select voter).ToListAsync(cancellationToken);
        var votes = await (
            from vote in _db.SessionVotes
            join session in _db.Sessions on vote.SessionId equals session.Id
            where session.GroupId == groupId
            select vote).ToListAsync(cancellationToken);
        var attendance = await (
            from row in _db.SessionAttendance
            join session in _db.Sessions on row.SessionId equals session.Id
            where session.GroupId == groupId && row.MembershipId == memberId
            select row).ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            var sessionVotes = votes.Where(vote => vote.SessionId == session.Id).ToList();
            var voted = sessionVotes.Any(vote => vote.VoterMembershipId == memberId);
            if (!voted)
            {
                var voter = voters.FirstOrDefault(row => row.SessionId == session.Id && row.MembershipId == memberId);
                if (voter is not null)
                {
                    _db.SessionVoters.Remove(voter);
                    voters.Remove(voter);
                }

                if (sessionVotes.Count == 0)
                {
                    var row = attendance.FirstOrDefault(item => item.SessionId == session.Id);
                    if (row is not null)
                        _db.SessionAttendance.Remove(row);
                }
            }

            var remaining = voters.Where(row => row.SessionId == session.Id).Select(row => row.MembershipId).ToList();
            if (!VotingSetRules.CanElect(remaining.Count))
            {
                session.FacilitatorMembershipId = null;
                continue;
            }

            var votedIds = sessionVotes.Select(vote => vote.VoterMembershipId).ToHashSet();
            if (session.FacilitatorMembershipId is null && remaining.All(votedIds.Contains))
            {
                var ballots = sessionVotes.Select(vote => (vote.VoterMembershipId, vote.CandidateMembershipId)).ToList();
                Elect(session, remaining, ballots, _tieBreak);
            }
        }
    }

    private async Task<CommandOutcome> Refuse(
        string attempt,
        AccountId accountId,
        GroupId groupId,
        SessionId sessionId,
        MemberId? memberId,
        CommandOutcome outcome,
        CancellationToken cancellationToken)
    {
        await RecordAsync(new ActivityWrite(attempt, false, accountId.Value, groupId.Value, sessionId.Value, memberId?.Value), cancellationToken);
        _log.Finish();
        return outcome;
    }

    private Task RecordAsync(ActivityWrite write, CancellationToken cancellationToken) =>
        _log.RecordAsync(write, cancellationToken);

    private static void Elect(
        Session session,
        IReadOnlyList<MemberId> votingSet,
        IReadOnlyList<(MemberId Voter, MemberId Candidate)> ballots,
        IFacilitatorTieBreak tieBreak)
    {
        var counts = votingSet.ToDictionary(id => id, _ => 0);
        foreach (var ballot in ballots)
        {
            counts.TryAdd(ballot.Candidate, 0);
            counts[ballot.Candidate]++;
        }

        var max = counts.Values.Max();
        var leaders = counts
            .Where(pair => pair.Value == max)
            .Select(pair => pair.Key)
            .OrderBy(id => id.Value)
            .ToList();
        session.FacilitatorMembershipId = leaders.Count == 1 ? leaders[0] : tieBreak.Choose(leaders);
    }

}
