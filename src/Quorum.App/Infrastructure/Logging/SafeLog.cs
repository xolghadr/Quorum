using Quorum.App.SharedKernel;

namespace Quorum.App.Infrastructure.Logging;

public sealed class SafeLog
{
    private readonly ILogger _logger;

    public SafeLog(ILoggerFactory factory)
    {
        _logger = factory.CreateLogger("Quorum");
    }

    public void Command(
        string command,
        AccountId? accountId = null,
        GroupId? groupId = null,
        SessionId? sessionId = null,
        string? topicKey = null)
    {
        _logger.LogInformation(
            "command {Command} account {AccountId} group {GroupId} session {SessionId} topic {TopicKey}",
            command,
            accountId?.Value,
            groupId?.Value,
            sessionId?.Value,
            topicKey);
    }

    public void Publication(string sourceDirectory, int tracks, int topics, int designs, Guid publicationId)
    {
        _logger.LogInformation(
            "command {Command} source {Source} tracks {Tracks} topics {Topics} designs {Designs} publication {PublicationId}",
            "publish-catalog",
            sourceDirectory,
            tracks,
            topics,
            designs,
            publicationId);
    }
}
