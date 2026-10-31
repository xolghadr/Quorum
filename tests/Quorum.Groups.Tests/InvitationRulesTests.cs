using Quorum.App.Features.Groups;

namespace Quorum.Groups.Tests;

public class InvitationRulesTests
{
    [Fact]
    public void Display_name_accepts_one_to_forty_characters()
    {
        Assert.False(Membership.DisplayNameAccepted(null, out _));
        Assert.False(Membership.DisplayNameAccepted("   ", out _));
        Assert.False(Membership.DisplayNameAccepted(new string('a', 41), out _));

        Assert.True(Membership.DisplayNameAccepted(" A ", out var one));
        Assert.Equal("A", one);
        Assert.True(Membership.DisplayNameAccepted(new string('b', 40), out var forty));
        Assert.Equal(40, forty.Length);
    }

    [Fact]
    public void Cap_counts_active_account_memberships_and_pending_invitations()
    {
        Assert.True(GroupCap.Fits(7, 1));
        Assert.True(GroupCap.Fits(8, 0));
        Assert.False(GroupCap.Fits(8, 1));
        Assert.False(GroupCap.Fits(7, 2));
    }
}
