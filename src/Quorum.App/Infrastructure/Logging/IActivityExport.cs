namespace Quorum.App.Infrastructure.Logging;

public interface IActivityExport
{
    Task<bool> SendAsync(ActivityExportBatch batch, CancellationToken cancellationToken);
}

public sealed class ActivityExportBatch
{
    public const string Span = "quorum.attempt";
    public const string Attempts = "quorum.attempts";
    public const string Duration = "quorum.attempt.duration";
    public const string StorageWait = "quorum.storage.wait";

    public static readonly IReadOnlySet<string> Allowlist = new HashSet<string>(StringComparer.Ordinal)
    {
        "quorum.attempt",
        "quorum.outcome",
        "quorum.reason",
        "quorum.reference",
        "quorum.duration_ms",
        "quorum.storage_wait_ms",
        "quorum.account_id",
        "quorum.group_id",
        "quorum.session_id",
        "quorum.member_id",
        "quorum.tracks",
        "quorum.topics",
        "quorum.designs",
        "service.name",
    };

    public required string Endpoint { get; init; }
    public required string SpanName { get; init; }
    public required string Reference { get; init; }
    public required IReadOnlyDictionary<string, string> ResourceAttributes { get; init; }
    public required IReadOnlyList<ActivityExportRow> Rows { get; init; }
}

public sealed class ActivityExportRow
{
    public required string Attempt { get; init; }
    public required string Outcome { get; init; }
    public required double DurationMs { get; init; }
    public required double StorageWaitMs { get; init; }
    public required DateTimeOffset AcceptedAt { get; init; }
    public required IReadOnlyDictionary<string, string> Attributes { get; init; }
}
