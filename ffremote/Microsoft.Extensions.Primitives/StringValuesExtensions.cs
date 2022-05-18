namespace Microsoft.Extensions.Primitives;

internal static class StringValuesExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string[] Split(this StringValues values, params char[]? separator) => values.SelectMany(value => value.Split(separator)).ToArray();
}
