namespace System.Collections.Generic;

internal static class CollectionExtensions
{
    public static void Add<T>(this ICollection<T> collection, params T[] items)
    {
        foreach (var item in items)
            collection.Add(item);
    }
}
