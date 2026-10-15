using Microsoft.AspNetCore.Http;

namespace DevBox.Api;

public static class SessionAuth
{
    public const string HeaderName = "Authorization";
    public const string QueryName = "token";

    public static string Token { get; private set; } = Guid.NewGuid().ToString("N");

    public static void Reset() => Token = Guid.NewGuid().ToString("N");

    public static bool IsAuthorized(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var header))
        {
            var value = header.ToString();
            if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                && string.Equals(value["Bearer ".Length..].Trim(), Token, StringComparison.Ordinal))
                return true;
        }

        if (context.Request.Query.TryGetValue(QueryName, out var query)
            && string.Equals(query.ToString(), Token, StringComparison.Ordinal))
            return true;

        return false;
    }
}
