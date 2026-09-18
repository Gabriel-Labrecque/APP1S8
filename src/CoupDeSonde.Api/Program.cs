using System.Threading.RateLimiting;
using CoupDeSonde.Api.Data;
using CoupDeSonde.Api.Models;
using CoupDeSonde.Api.Security;
using CoupDeSonde.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Coup de Sonde - API de sondage",
        Version = "v1",
        Description = "API securisee pour la gestion de sondages (GEI-771)."
    });

    var apiKeyScheme = new OpenApiSecurityScheme
    {
        Name = ApiKeyAuthenticationOptions.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Clef d'API requise pour toutes les requetes.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
    };
    options.AddSecurityDefinition("ApiKey", apiKeyScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { apiKeyScheme, Array.Empty<string>() }
    });
});

// Resolue au moment de la premiere utilisation (pas ici) afin que les overrides de
// configuration ajoutes par WebApplicationFactory (tests) soient bien pris en compte :
// builder.Configuration lu avant Build() ne verrait pas ces overrides.
static string ResoudreDataDirectory(IServiceProvider sp)
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var environment = sp.GetRequiredService<IWebHostEnvironment>();
    return configuration["DataDirectory"] ?? Path.Combine(environment.ContentRootPath, "Data");
}

builder.Services.AddSingleton(sp => new FileSondageRepository(ResoudreDataDirectory(sp)));
builder.Services.AddSingleton(sp => new FileParticipantRepository(ResoudreDataDirectory(sp)));
builder.Services.AddSingleton<IPasswordHasher<Participant>, PasswordHasher<Participant>>();
builder.Services.AddScoped<ISondageService, SondageService>();
builder.Services.AddScoped<IParticipantAuthService, ParticipantAuthService>();

builder.Services
    .AddAuthentication(ApiKeyAuthenticationOptions.DefaultScheme)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationOptions.DefaultScheme, _ => { })
    .AddCookie(ParticipantCookieDefaults.Scheme, options =>
    {
        options.Cookie.Name = "CoupDeSonde.Participant";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;

        // API : jamais de redirection HTML vers une page de login, seulement un code de statut.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

// Limite les tentatives de connexion (par IP) pour contrer le brute-force en ligne du mot
// de passe d'un participant. Configurable pour permettre aux tests de l'isoler du reste
// de la suite (le seuil de production reel est la valeur par defaut ci-dessous : 10/min).
builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return ValueTask.CompletedTask;
    };

    options.AddPolicy("connexion", httpContext =>
    {
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        var limite = configuration.GetValue("RateLimiting:ConnexionPermitLimit", 10);

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ConnexionRateLimiting.CleDePartition(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = limite,
                QueueLimit = 0
            });
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();

public partial class Program { }
