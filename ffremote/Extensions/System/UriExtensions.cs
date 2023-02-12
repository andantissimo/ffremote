namespace System;

internal static class UriExtensions
{
    public static Uri SetScheme(this Uri uri, string scheme)
    {
        if (!uri.IsAbsoluteUri)
            throw new ArgumentException("Absolute URI required");

        return new UriBuilder(scheme, uri.Host, uri.Port, uri.AbsolutePath) { Query = uri.Query }.Uri;
    }

    public static Uri ToHttpUri(this Uri uri) => uri.SetScheme(uri.Scheme.EndsWith('s') ? "https" : "http");

    public static Uri ToWebSocketUri(this Uri uri) => uri.SetScheme(uri.Scheme.EndsWith('s') ? "wss" : "ws");
}
