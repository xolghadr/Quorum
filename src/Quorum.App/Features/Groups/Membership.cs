using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Groups;

public sealed class Membership
{
    public const int DisplayNameMax = 40;

    public MemberId Id { get; set; }
    public GroupId GroupId { get; set; }
    public AccountId? AccountId { get; set; }
    public string DisplayName { get; set; } = "";
    public string DisplayNameKey { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }

    public bool IsActive => RemovedAt is null;

    public static bool DisplayNameAccepted(string? name, out string trimmed)
    {
        trimmed = name?.Trim() ?? "";
        return trimmed.Length is >= 1 and <= DisplayNameMax;
    }

    public static string KeyFor(string trimmed) => trimmed.ToLowerInvariant();
}
