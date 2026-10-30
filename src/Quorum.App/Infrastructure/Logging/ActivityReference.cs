namespace Quorum.App.Infrastructure.Logging;

public static class ActivityReference
{
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Generate(Random? random = null)
    {
        var source = random ?? Random.Shared;
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[source.Next(Alphabet.Length)];
        return new string(chars);
    }

    public static bool Matches(string? value) =>
        value is { Length: 8 } && value.All(Alphabet.Contains);
}
