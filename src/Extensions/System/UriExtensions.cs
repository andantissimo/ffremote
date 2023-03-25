namespace System;

internal static class UriExtensions
{
    public static Uri WithScheme(this Uri uri, string scheme)
    {
        if (!uri.IsAbsoluteUri)
            throw new ArgumentException("Absolute URI required");

        return new UriBuilder(scheme, uri.Host, uri.Port, uri.AbsolutePath) { Query = uri.Query }.Uri;
    }

    public static Uri WithHost(this Uri uri, string host)
    {
        if (!uri.IsAbsoluteUri)
            throw new ArgumentException("Absolute URI required");

        return new UriBuilder(uri.Scheme, host, uri.Port, uri.AbsolutePath) { Query = uri.Query }.Uri;
    }

    public static Uri AsHttpUri(this Uri uri) => uri.WithScheme(uri.Scheme.EndsWith('s') ? "https" : "http");

    public static Uri AsWebSocketUri(this Uri uri) => uri.WithScheme(uri.Scheme.EndsWith('s') ? "wss" : "ws");
}
