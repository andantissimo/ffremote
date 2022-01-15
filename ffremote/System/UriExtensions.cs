namespace System;

internal static class UriExtensions
{
    public static Uri ToHttpUri(this Uri uri)
    {
        if (!uri.IsAbsoluteUri)
            throw new ArgumentException("Absolute URI required");

        return new UriBuilder
        {
            Scheme = uri.Scheme.EndsWith('s') ? "https" : "http",
            Host = uri.Host,
            Port = uri.Port,
            Path = uri.AbsolutePath,
            Query = uri.Query,
        }.Uri;
    }

    public static Uri ToWebSocketUri(this Uri uri)
    {
        if (!uri.IsAbsoluteUri)
            throw new ArgumentException("Absolute URI required");

        return new UriBuilder
        {
            Scheme = uri.Scheme.EndsWith('s') ? "wss" : "ws",
            Host = uri.Host,
            Port = uri.Port,
            Path = uri.AbsolutePath,
            Query = uri.Query,
        }.Uri;
    }
}
