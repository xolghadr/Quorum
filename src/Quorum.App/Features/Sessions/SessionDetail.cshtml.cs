using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Auth;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Sessions;

public sealed class SessionDetailModel : SignedInPageModel
{
    private readonly QuorumDbContext _db;
    private readonly SafeLog _log;

    public SessionDetailModel(QuorumDbContext db, SafeLog log)
    {
        _db = db;
        _log = log;
    }

    public Guid SessionId { get; private set; }
    public int Number { get; private set; }
    public DateOnly CreatedOn { get; private set; }
    public string Facilitator { get; private set; } = "not elected";
    public bool CanEditTopics { get; private set; }
    public bool VotingLocked { get; private set; }
    public IReadOnlyList<TopicRow> Topics { get; private set; } = [];
    public IReadOnlyList<PersonRow> Present { get; private set; } = [];
    public IReadOnlyList<PersonRow> Candidates { get; private set; } = [];
    public Guid? SelfId { get; private set; }
    public bool SelfPresent { get; private set; }
    public bool CanVote { get; private set; }
    public IReadOnlyList<VoteRow> Votes { get; private set; } = [];
    public IReadOnlyList<TopicChoice> Chooser { get; private set; } = [];
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (SignInSession.Account(User) is not { } accountId)
            return Challenge();

        var id = new SessionId(sessionId);
        var session = await _db.Sessions.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (session is null)
            return NotFound();

        var belongs = await _db.Memberships.AnyAsync(
            member => member.AccountId == accountId && member.GroupId == session.GroupId && member.RemovedAt == null,
            cancellationToken);
        if (!belongs || OpenGroupId != session.GroupId)
            return NotFound();

        SessionId = sessionId;
        Number = session.Number;
        CreatedOn = session.CreatedOn;
        VotingLocked = session.FacilitatorMembershipId is not null;
        if (session.FacilitatorMembershipId is { } facilitatorId)
        {
            Facilitator = await _db.Memberships
                .Where(member => member.Id == facilitatorId)
                .Select(member => member.DisplayName)
                .FirstOrDefaultAsync(cancellationToken) ?? "not elected";
        }

        CanEditTopics = session.CreatedByAccountId == AccountId && session.FacilitatorMembershipId is null;

        Topics = await (
            from link in _db.SessionTopics
            join topic in _db.Topics on link.TopicId equals topic.Id
            where link.SessionId == id
            orderby link.Position
            select new TopicRow(topic.Key, topic.Title)).ToListAsync(cancellationToken);

        var self = await _db.Memberships.FirstOrDefaultAsync(
            member => member.GroupId == OpenGroupId && member.AccountId == AccountId && member.RemovedAt == null,
            cancellationToken);
        SelfId = self?.Id.Value;

        Present = await (
            from row in _db.SessionAttendance
            join member in _db.Memberships on row.MembershipId equals member.Id
            where row.SessionId == id
            select new PersonRow(member.Id.Value, member.DisplayName)).ToListAsync(cancellationToken);

        var voters = await (
            from row in _db.SessionVoters
            join member in _db.Memberships on row.MembershipId equals member.Id
            where row.SessionId == id
            select new PersonRow(member.Id.Value, member.DisplayName)).ToListAsync(cancellationToken);

        var eligiblePresent = await (
            from row in _db.SessionAttendance
            join member in _db.Memberships on row.MembershipId equals member.Id
            where row.SessionId == id && member.AccountId != null && member.RemovedAt == null
            select new PersonRow(member.Id.Value, member.DisplayName)).ToListAsync(cancellationToken);

        Votes = await (
            from vote in _db.SessionVotes
            join voter in _db.Memberships on vote.VoterMembershipId equals voter.Id
            join candidate in _db.Memberships on vote.CandidateMembershipId equals candidate.Id
            where vote.SessionId == id
            select new VoteRow(voter.Id.Value, voter.DisplayName, candidate.DisplayName)).ToListAsync(cancellationToken);

        SelfPresent = self is not null && Present.Any(person => person.Id == self.Id.Value);
        var hasVoted = self is not null && Votes.Any(vote => vote.VoterId == self.Id.Value);
        var opening = voters.Count == 0
            && self is not null
            && eligiblePresent.Any(person => person.Id == self.Id.Value)
            && eligiblePresent.Count is >= 2 and <= Group.MemberMax;
        CanVote = !VotingLocked && self is not null && !hasVoted && (voters.Any(person => person.Id == self.Id.Value) || opening);
        Candidates = voters.Count > 0 ? voters : eligiblePresent;

        if (CanEditTopics)
            Chooser = await SessionCommands.ChooserAsync(_db, OpenGroupId!.Value, cancellationToken);

        if (Request.Query.ContainsKey("error"))
        {
            _log.AdoptReference(Request.Query["reference"]);
            Error = "That change was not saved.";
        }
        return Page();
    }

    public sealed record TopicRow(string Key, string Title);
    public sealed record PersonRow(Guid Id, string DisplayName);
    public sealed record VoteRow(Guid VoterId, string Voter, string Candidate);
}
