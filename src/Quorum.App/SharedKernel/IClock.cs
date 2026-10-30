namespace Quorum.App.SharedKernel;

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateOnly Today { get; }
}

public readonly record struct ContentKey(string Value)
{
    public static ContentKey From(string value) => new(value);

    public override string ToString() => Value;
}
