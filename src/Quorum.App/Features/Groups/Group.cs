using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Groups;

public sealed class Group
{
    public const int NameMax = 60;
    public const int MemberMax = 8;

    public GroupId Id { get; set; }
    public AccountId CreatorAccountId { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }

    public static bool NameAccepted(string? name, out string trimmed)
    {
        trimmed = name?.Trim() ?? "";
        return trimmed.Length is >= 1 and <= NameMax;
    }
}
