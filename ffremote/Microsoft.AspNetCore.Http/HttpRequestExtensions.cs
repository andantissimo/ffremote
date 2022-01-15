namespace Microsoft.AspNetCore.Http;

internal static class HttpRequestExtensions
{
    public static bool TryGetBasicAuthenticationCredentials(this HttpRequest request,
        [NotNullWhen(returnValue: true)] out string? username, [NotNullWhen(returnValue: true)] out string? password)
    {
        username = default;
        password = default;

        if (!System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var auth))
            return false;
        if (auth.Scheme?.Equals("Basic", StringComparison.OrdinalIgnoreCase) != true || auth.Parameter is null)
            return false;
        var buffer = new byte[80];
        if (!Convert.TryFromBase64String(auth.Parameter, buffer, out var length))
            return false;
        (username, password) = Encoding.UTF8.GetString(buffer, 0, length).Split(':', 2);

        return password is not null;
    }
}
