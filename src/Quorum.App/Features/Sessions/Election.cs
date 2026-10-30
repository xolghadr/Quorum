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

    public Task<CommandOutcome> SaveAttendanceAsync(
        GroupId groupId,
        AccountId accountId,
        SessionId sessionId,
        IReadOnlyList<MemberId> memberIds,
        CancellationToken cancellationToken = default) =>
        SaveAttendanceAsync(groupId, accountId, sessionId, memberIds, _db, cancellationToken);

    public async Task<CommandOutcome> SaveAttendanceAsync(
        GroupId groupId,
        AccountId accountId,
        SessionId sessionId,
        IReadOnlyList<MemberId> memberIds,
        QuorumDbContext db,
        CancellationToken cancellationToken = default)
    {
        _log.Begin();
        if (!await IsMember(db, groupId, accountId, cancellationToken))
            return await Refuse("save-attendance", accountId, groupId, sessionId, memberId: null, CommandOutcome.NotFound(), cancellationToken);

        var session = await db.Sessions.FirstOrDefaultAsync(
            row => row.Id == sessionId && row.GroupId == groupId,
            cancellationToken);
        if (session is null)
            return await Refuse("save-attendance", accountId, groupId, sessionId, memberId: null, CommandOutcome.NotFound(), cancellationToken);

        var distinct = memberIds.Distinct().ToList();
        if (distinct.Count != memberIds.Count)
            return await Refuse("save-attendance", accountId, groupId, sessionId, memberId: null, CommandOutcome.Refused(), cancellationToken);

        var active = await db.Memberships
            .Where(member => member.GroupId == groupId && member.RemovedAt == null)
            .Select(member => member.Id)
            .ToListAsync(cancellationToken);
        if (distinct.Any(id => !active.Contains(id)) || distinct.Count > active.Count || distinct.Count > Group.MemberMax)
            return await Refuse("save-attendance", accountId, groupId, sessionId, memberId: null, CommandOutcome.Refused(), cancellationToken);

        var votingOpen = await db.SessionVoters.AnyAsync(voter => voter.SessionId == sessionId, cancellationToken);
        if (!votingOpen && distinct.Count is < 2 or > Group.MemberMax)
            return await Refuse("save-attendance", accountId, groupId, sessionId, memberId: null, CommandOutcome.Refused(), cancellationToken);

        var existing = await db.SessionAttendance.Where(row => row.SessionId == sessionId).ToListAsync(cancellationToken);
        db.SessionAttendance.RemoveRange(existing);
        foreach (var memberId in distinct)
        {
            db.SessionAttendance.Add(new SessionAttendance
            {
                SessionId = sessionId,
                MembershipId = memberId,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await RecordAsync(new ActivityWrite("save-attendance", true, accountId.Value, groupId.Value, sessionId.Value), cancellationToken);
        _log.Finish();
        _log.Command("save-attendance", accountId, groupId, sessionId);
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
        if (!await IsMember(_db, groupId, accountId, cancellationToken))
            return await Refuse("cast-vote", accountId, groupId, sessionId, voterId, CommandOutcome.NotFound(), cancellationToken);

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
            var attendance = await _db.SessionAttendance
                .Where(row => row.SessionId == sessionId)
                .Select(row => row.MembershipId)
                .ToListAsync(cancellationToken);
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
            if (counts.ContainsKey(ballot.Candidate))
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

    private static async Task<bool> IsMember(
        QuorumDbContext db,
        GroupId groupId,
        AccountId accountId,
        CancellationToken cancellationToken) =>
        await db.Memberships.AnyAsync(
            member => member.GroupId == groupId && member.AccountId == accountId && member.RemovedAt == null,
            cancellationToken);
}
