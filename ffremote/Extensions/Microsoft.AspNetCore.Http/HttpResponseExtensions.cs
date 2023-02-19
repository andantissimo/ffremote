namespace Microsoft.AspNetCore.Http;

internal static class HttpResponseExtensions
{
    public static void End(this HttpResponse response, int statusCode)
    {
        if (response.HasStarted)
            return;

        response.Clear();
        response.ContentLength = 0;
        response.StatusCode = statusCode;
    }
}
