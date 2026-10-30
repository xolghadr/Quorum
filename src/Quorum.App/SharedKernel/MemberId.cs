namespace Quorum.App.SharedKernel;

public readonly record struct MemberId(Guid Value)
{
    public static MemberId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
