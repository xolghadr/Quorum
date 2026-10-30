using System.Diagnostics;

namespace Quorum.App.Infrastructure.Logging;

public sealed class ActivitySubmission
{
    public ActivitySubmission(string reference)
    {
        Reference = reference;
        Watch = Stopwatch.StartNew();
    }

    public string Reference { get; set; }
    public Stopwatch Watch { get; }
    public bool Reserved { get; set; }
    public bool Finished { get; set; }
}

public sealed record ActivityWrite(
    string Attempt,
    bool Succeeded,
    Guid? AccountId = null,
    Guid? GroupId = null,
    Guid? SessionId = null,
    Guid? MemberId = null,
    string? Reason = null,
    int? Tracks = null,
    int? Topics = null,
    int? Designs = null);
