using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Groups;

public sealed class Invitation
{
    public const int EmailMax = 320;

    public InvitationId Id { get; set; }
    public GroupId GroupId { get; set; }
    public string EmailKey { get; set; } = "";
    public AccountId InvitedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? DeclinedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public bool IsPending => AcceptedAt is null && DeclinedAt is null && CancelledAt is null;
}

public static class GroupCap
{
    public static bool Fits(int activeAccountMemberships, int pendingInvitations) =>
        activeAccountMemberships + pendingInvitations <= Group.MemberMax;
}
