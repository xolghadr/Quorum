namespace Quorum.App.SharedKernel;

public readonly record struct GroupId(Guid Value)
{
    public static GroupId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
