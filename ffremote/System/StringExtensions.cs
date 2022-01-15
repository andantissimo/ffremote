internal static class StringExtensions
{
    public static void Deconstruct(this string[]? pair, out string? first, out string? second)
    {
        first = pair is { Length: > 0 } ? pair[0] : null;
        second = pair is { Length: > 1 } ? pair[1] : null;
    }
}
