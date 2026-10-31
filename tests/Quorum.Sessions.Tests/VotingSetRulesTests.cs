using Quorum.App.Features.Sessions;

namespace Quorum.Sessions.Tests;

public class VotingSetRulesTests
{
    [Fact]
    public void Fewer_than_two_remaining_voters_leaves_the_facilitator_unset()
    {
        // If fewer than two SessionVoter rows remain, leave FacilitatorMembershipId null.
        Assert.False(VotingSetRules.CanElect(0));
        Assert.False(VotingSetRules.CanElect(1));
        Assert.True(VotingSetRules.CanElect(2));
        Assert.True(VotingSetRules.CanElect(8));
    }
}
