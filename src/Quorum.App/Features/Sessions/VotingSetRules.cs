namespace Quorum.App.Features.Sessions;

public static class VotingSetRules
{
    public static bool CanElect(int remainingVoters) => remainingVoters >= 2;
}
