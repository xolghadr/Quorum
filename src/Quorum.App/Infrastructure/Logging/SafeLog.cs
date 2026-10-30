using Microsoft.EntityFrameworkCore;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Infrastructure.Logging;

public sealed class SafeLog
{
    private const string ReferenceItem = "quorum.reference";

    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _http;
    private readonly AsyncLocal<ActivitySubmission?> _current = new();
    private readonly AsyncLocal<string?> _shown = new();

    public SafeLog(ILoggerFactory factory, IServiceScopeFactory scopes, IClock clock, IHttpContextAccessor http)
    {
        _logger = factory.CreateLogger("Quorum");
        _scopes = scopes;
        _clock = clock;
        _http = http;
    }

    public string? CurrentReference => _shown.Value ?? _current.Value?.Reference ?? RequestReference();

    public void Begin()
    {
        var existing = _current.Value;
        if (existing is not null && !existing.Finished)
            return;
        StorageWait.Reset();
        _shown.Value = null;
        var submission = new ActivitySubmission(ActivityReference.Generate());
        _current.Value = submission;
        Remember(submission.Reference);
    }

    public void Finish()
    {
        if (_current.Value is { } submission)
        {
            submission.Watch.Stop();
            submission.Finished = true;
        }
    }

    public void AdoptReference(string? reference)
    {
        if (!ActivityReference.Matches(reference))
            return;
        _shown.Value = reference;
        Remember(reference!);
    }

    public string ReferenceParameter()
    {
        var reference = CurrentReference;
        return ActivityReference.Matches(reference) ? "&reference=" + reference : "";
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

    public async Task RecordAsync(ActivityWrite write, CancellationToken cancellationToken = default)
    {
        Validate(write);
        Begin();
        var submission = _current.Value ?? throw new InvalidOperationException("An activity submission is required.");
        var duration = (int)Math.Min(int.MaxValue, StorageWait.Time(submission.Watch));
        var storageWait = (int)Math.Min(duration, StorageWait.Milliseconds);

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QuorumDbContext>();
        if (!submission.Reserved)
        {
            var reference = submission.Reference;
            while (await db.ActivityRecords.AnyAsync(row => row.Reference == reference, cancellationToken))
                reference = ActivityReference.Generate();
            submission.Reference = reference;
            Remember(reference);
        }

        db.ActivityRecords.Add(new ActivityRecord
        {
            Id = Guid.CreateVersion7(),
            Reference = submission.Reference,
            Attempt = write.Attempt,
            Outcome = write.Succeeded ? ActivityRecord.Succeeded : ActivityRecord.Refused,
            Reason = write.Reason,
            AcceptedAt = _clock.UtcNow,
            DurationMs = duration,
            StorageWaitMs = storageWait,
            AccountId = write.AccountId,
            GroupId = write.GroupId,
            SessionId = write.SessionId,
            MemberId = write.MemberId,
            Tracks = write.Tracks,
            Topics = write.Topics,
            Designs = write.Designs,
            ExportedAt = null,
        });
        await db.SaveChangesAsync(cancellationToken);
        submission.Reserved = true;
        Remember(submission.Reference);
    }

    private string? RequestReference()
    {
        var value = _http.HttpContext?.Items[ReferenceItem] as string;
        return ActivityReference.Matches(value) ? value : null;
    }

    private void Remember(string reference)
    {
        if (_http.HttpContext is { } http)
            http.Items[ReferenceItem] = reference;
    }

    private static void Validate(ActivityWrite write)
    {
        if (!ActivityRecord.Attempts.Contains(write.Attempt))
            throw new ArgumentOutOfRangeException(nameof(write), "The attempt name is not recorded.");
        if (write.Reason is not null && write.Reason != ActivityRecord.UnknownAccount)
            throw new ArgumentOutOfRangeException(nameof(write), "The reason is not recorded.");
        if (write.Reason == ActivityRecord.UnknownAccount && write.Attempt != "sign-in")
            throw new ArgumentOutOfRangeException(nameof(write), "unknown-account is only for sign-in.");
        var counts = write.Tracks is not null || write.Topics is not null || write.Designs is not null;
        if (counts && (write.Attempt != "publish-catalog" || !write.Succeeded))
            throw new ArgumentOutOfRangeException(nameof(write), "Counts are only stored for a succeeded publication.");
    }
}
