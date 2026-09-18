using Microsoft.AspNetCore.Http;

namespace CoupDeSonde.Api.Security;

public static class ConnexionRateLimiting
{
    public static string CleDePartition(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "inconnu";
}
