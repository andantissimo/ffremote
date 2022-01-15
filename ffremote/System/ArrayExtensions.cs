internal static class ArrayExtensions
{
    public static void Deconstruct<T>(this T[] pair, out T first, out T second)
    {
        first = pair[0];
        second = pair[1];
    }
}
