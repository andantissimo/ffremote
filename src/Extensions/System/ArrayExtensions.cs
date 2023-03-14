internal static class ArrayExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Deconstruct<T>(this T[]? pair, out T? first, out T? second) where T : class
    {
        (first, second) = (pair?.ElementAtOrDefault(0), pair?.ElementAtOrDefault(1));
    }
}
