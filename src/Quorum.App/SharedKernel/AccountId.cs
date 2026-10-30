namespace Quorum.App.SharedKernel;

public readonly record struct AccountId(Guid Value)
{
    public static AccountId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
