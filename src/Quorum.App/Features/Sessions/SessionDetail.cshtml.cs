using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Sessions;

public sealed class SessionDetailModel : SignedInPageModel
{
    private readonly QuorumDbContext _db;

    public SessionDetailModel(QuorumDbContext db)
    {
        _db = db;
    }

    public Guid SessionId { get; private set; }
    public int Number { get; private set; }
    public DateOnly CreatedOn { get; private set; }
    public string Facilitator { get; private set; } = "not elected";
    public bool CanEditTopics { get; private set; }
    public bool VotingLocked { get; private set; }
    public IReadOnlyList<TopicRow> Topics { get; private set; } = [];
    public IReadOnlyList<PersonRow> ActiveMembers { get; private set; } = [];
    public IReadOnlyList<PersonRow> Present { get; private set; } = [];
    public IReadOnlyList<PersonRow> Voters { get; private set; } = [];
    public IReadOnlyList<VoteRow> Votes { get; private set; } = [];
    public IReadOnlyList<TopicChoice> Chooser { get; private set; } = [];
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var gate = await RequireGroupAsync(_db, cancellationToken);
        if (gate is not null)
            return gate;

        var id = new SessionId(sessionId);
        var session = await _db.Sessions.FirstOrDefaultAsync(
            row => row.Id == id && row.GroupId == OpenGroupId,
            cancellationToken);
        if (session is null)
            return NotFound();

        var group = await _db.Groups.FirstAsync(row => row.Id == OpenGroupId, cancellationToken);
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

        var isCreator = group.CreatorAccountId == AccountId;
        CanEditTopics = session.Number == 1
            ? session.FacilitatorMembershipId is null && isCreator
            : session.FacilitatorMembershipId is not null;

        Topics = await (
            from link in _db.SessionTopics
            join topic in _db.Topics on link.TopicId equals topic.Id
            where link.SessionId == id
            orderby link.Position
            select new TopicRow(topic.Key, topic.Title)).ToListAsync(cancellationToken);

        ActiveMembers = await _db.Memberships
            .Where(member => member.GroupId == OpenGroupId && member.RemovedAt == null)
            .OrderBy(member => member.CreatedAt)
            .Select(member => new PersonRow(member.Id.Value, member.DisplayName))
            .ToListAsync(cancellationToken);

        Present = await (
            from row in _db.SessionAttendance
            join member in _db.Memberships on row.MembershipId equals member.Id
            where row.SessionId == id
            select new PersonRow(member.Id.Value, member.DisplayName)).ToListAsync(cancellationToken);

        Voters = await (
            from row in _db.SessionVoters
            join member in _db.Memberships on row.MembershipId equals member.Id
            where row.SessionId == id
            select new PersonRow(member.Id.Value, member.DisplayName)).ToListAsync(cancellationToken);

        Votes = await (
            from vote in _db.SessionVotes
            join voter in _db.Memberships on vote.VoterMembershipId equals voter.Id
            join candidate in _db.Memberships on vote.CandidateMembershipId equals candidate.Id
            where vote.SessionId == id
            select new VoteRow(voter.Id.Value, voter.DisplayName, candidate.DisplayName)).ToListAsync(cancellationToken);

        if (CanEditTopics)
            Chooser = await SessionCommands.ChooserAsync(_db, OpenGroupId!.Value, cancellationToken);

        if (Request.Query.ContainsKey("error"))
            Error = "That change was not saved.";
        return Page();
    }

    public sealed record TopicRow(string Key, string Title);
    public sealed record PersonRow(Guid Id, string DisplayName);
    public sealed record VoteRow(Guid VoterId, string Voter, string Candidate);
}
