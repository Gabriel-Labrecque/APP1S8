using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CoupDeSonde.Api.Security;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public const string HeaderName = "X-API-Key";
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var cleFournie)
            || string.IsNullOrWhiteSpace(cleFournie))
        {
            return Task.FromResult(AuthenticateResult.Fail("Cle d'API manquante."));
        }

        var clesValides = _configuration.GetSection("ApiKeys").Get<string[]>() ?? Array.Empty<string>();

        if (!clesValides.Contains(cleFournie.ToString()))
        {
            return Task.FromResult(AuthenticateResult.Fail("Cle d'API invalide."));
        }

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "ClientApi") }, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
