using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Sessions;

public sealed class Session
{
    public SessionId Id { get; set; }
    public GroupId GroupId { get; set; }
    public int Number { get; set; }
    public AccountId CreatedByAccountId { get; set; }
    public DateOnly CreatedOn { get; set; }
    public MemberId? FacilitatorMembershipId { get; set; }
}

public sealed class SessionAttendance
{
    public SessionId SessionId { get; set; }
    public MemberId MembershipId { get; set; }
}

public sealed class SessionVoter
{
    public SessionId SessionId { get; set; }
    public MemberId MembershipId { get; set; }
}

public sealed class SessionVote
{
    public SessionId SessionId { get; set; }
    public MemberId VoterMembershipId { get; set; }
    public MemberId CandidateMembershipId { get; set; }
}

public sealed class SessionTopic
{
    public SessionId SessionId { get; set; }
    public Guid TopicId { get; set; }
    public int Position { get; set; }
}

public sealed class TopicProgress
{
    public Guid Id { get; set; }
    public GroupId GroupId { get; set; }
    public Guid TopicId { get; set; }
    public int PickCount { get; set; }
}
