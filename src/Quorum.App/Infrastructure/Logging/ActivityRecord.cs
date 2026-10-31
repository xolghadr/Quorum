namespace Quorum.App.Infrastructure.Logging;

public sealed class ActivityRecord
{
    public const string Succeeded = "succeeded";
    public const string Refused = "refused";
    public const string UnknownAccount = "unknown-account";

    public static readonly IReadOnlySet<string> Attempts = new HashSet<string>(StringComparer.Ordinal)
    {
        "register",
        "sign-in",
        "sign-out",
        "create-group",
        "add-member",
        "invite-member",
        "cancel-invitation",
        "accept-invitation",
        "decline-invitation",
        "leave-group",
        "rename-member",
        "remove-member",
        "publish-catalog",
        "create-session",
        "set-topics",
        "save-attendance",
        "mark-attendance",
        "cast-vote",
        "elect-facilitator",
        "application-start",
        "application-stop",
        "health-ready",
        "health-not-ready",
    };

    public Guid Id { get; set; }
    public string Reference { get; set; } = "";
    public string Attempt { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string? Reason { get; set; }
    public DateTimeOffset AcceptedAt { get; set; }
    public int DurationMs { get; set; }
    public int StorageWaitMs { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? MemberId { get; set; }
    public int? Tracks { get; set; }
    public int? Topics { get; set; }
    public int? Designs { get; set; }
    public DateTimeOffset? ExportedAt { get; set; }
}
