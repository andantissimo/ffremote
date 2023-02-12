internal static class ArrayExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Deconstruct<T>(this T[] pair, out T first, out T second)
    {
        first = pair[0];
        second = pair[1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Deconstruct(this string[]? pair, out string? first, out string? second)
    {
        first = pair is { Length: > 0 } ? pair[0] : null;
        second = pair is { Length: > 1 } ? pair[1] : null;
    }
}
