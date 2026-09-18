using System.Net;
using System.Net.Http.Json;
using CoupDeSonde.Api.Models;
using CoupDeSonde.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoupDeSonde.Tests;

/// <summary>
/// Couvre les chemins defensifs qui ne peuvent pas etre atteints via le comportement
/// normal de l'API : statut de service inattendu (garde interne) et configuration
/// de securite absente (comportement "deny by default").
/// </summary>
public class EdgeCaseTests : IClassFixture<SondageApiFactory>
{
    private readonly SondageApiFactory _factory;

    public EdgeCaseTests(SondageApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostReponse_StatutDeServiceInattendu_Retourne500()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISondageService>();
                services.AddScoped<ISondageService, ServiceRetournantStatutInconnu>();
            });
        });

        // L'echange de service n'affecte pas l'authentification : on doit quand meme
        // s'inscrire et se connecter pour obtenir la session participant requise.
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", SondageApiFactory.ValidApiKey);
        var nomUtilisateur = "participant-" + Guid.NewGuid().ToString("N")[..8];
        await client.PostAsJsonAsync("/api/comptes/inscription", new InscriptionRequete(nomUtilisateur, "MotDePasse123!"));
        await client.PostAsJsonAsync("/api/comptes/connexion", new ConnexionRequete(nomUtilisateur, "MotDePasse123!"));

        var soumission = new ReponseSoumission { Reponses = new() { [1] = "a" } };
        var response = await client.PostAsJsonAsync("/api/sondages/1/reponses", soumission);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Inscription_StatutDeServiceInattendu_Retourne500()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IParticipantAuthService>();
                services.AddScoped<IParticipantAuthService, ServiceInscriptionRetournantStatutInconnu>();
            });
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", SondageApiFactory.ValidApiKey);

        var response = await client.PostAsJsonAsync(
            "/api/comptes/inscription",
            new InscriptionRequete("peu-importe", "MotDePasse123!"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Inscription_ServiceRetourneIdentifiantsInvalides_Retourne400()
    {
        // Prouve le mapping du controleur pour ce statut, meme si en pratique les DataAnnotations
        // interceptent deja les cas invalides avant d'atteindre le service (defense en profondeur).
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IParticipantAuthService>();
                services.AddScoped<IParticipantAuthService, ServiceInscriptionRetournantIdentifiantsInvalides>();
            });
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", SondageApiFactory.ValidApiKey);

        var response = await client.PostAsJsonAsync(
            "/api/comptes/inscription",
            new InscriptionRequete("peu-importe", "MotDePasse123!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AucuneCleApiConfiguree_RefuseParDefaut()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "coupdesonde-tests-noapikeys-" + Guid.NewGuid());

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                // Reconstruit une configuration minimale, volontairement sans section "ApiKeys",
                // pour verifier que l'absence de cles configurees refuse toutes les requetes.
                config.Sources.Clear();
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DataDirectory"] = dataDirectory
                });
            });
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "peu-importe");

        var response = await client.GetAsync("/api/sondages");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SansOverrideDataDirectory_UtiliseLesDonneesReellesDuProjet()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.Sources.Clear();
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ApiKeys:0"] = SondageApiFactory.ValidApiKey
                    // Pas de "DataDirectory" ici : force le repli sur ContentRootPath/Data.
                });
            });
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", SondageApiFactory.ValidApiKey);

        var response = await client.GetAsync("/api/sondages");

        response.EnsureSuccessStatusCode();
        var sondages = await response.Content.ReadFromJsonAsync<List<SondageResume>>();
        Assert.Equal(2, sondages!.Count);
    }

    [Fact]
    public async Task Connexion_TropDeTentatives_Retourne429()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                // Seuil volontairement tres bas, isole a ce test : n'affecte pas le reste
                // de la suite qui partage le seuil eleve defini dans SondageApiFactory.
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:ConnexionPermitLimit"] = "2"
                });
            });
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", SondageApiFactory.ValidApiKey);
        var requete = new ConnexionRequete("peu-importe", "peu-importe");

        await client.PostAsJsonAsync("/api/comptes/connexion", requete);
        await client.PostAsJsonAsync("/api/comptes/connexion", requete);
        var troisiemeTentative = await client.PostAsJsonAsync("/api/comptes/connexion", requete);

        Assert.Equal(HttpStatusCode.TooManyRequests, troisiemeTentative.StatusCode);
    }

    private class ServiceRetournantStatutInconnu : ISondageService
    {
        public Task<IReadOnlyList<SondageResume>> ListerSondagesAsync()
            => Task.FromResult<IReadOnlyList<SondageResume>>(new List<SondageResume>());

        public Task<Sondage?> ObtenirSondageAsync(int id) => Task.FromResult<Sondage?>(null);

        public Task<SubmissionResult> SoumettreReponseAsync(int sondageId, string participantId, ReponseSoumission soumission)
            => Task.FromResult(new SubmissionResult((SubmissionStatus)999));

        public Task<ResultatsSondage?> ObtenirResultatsAsync(int sondageId) => Task.FromResult<ResultatsSondage?>(null);
    }

    private class ServiceInscriptionRetournantStatutInconnu : IParticipantAuthService
    {
        public Task<RegistrationResult> InscrireAsync(string nomUtilisateur, string motDePasse)
            => Task.FromResult(new RegistrationResult((RegistrationStatus)999));

        public Task<bool> ValiderIdentifiantsAsync(string nomUtilisateur, string motDePasse) => Task.FromResult(false);
    }

    private class ServiceInscriptionRetournantIdentifiantsInvalides : IParticipantAuthService
    {
        public Task<RegistrationResult> InscrireAsync(string nomUtilisateur, string motDePasse)
            => Task.FromResult(new RegistrationResult(RegistrationStatus.IdentifiantsInvalides, "invalide"));

        public Task<bool> ValiderIdentifiantsAsync(string nomUtilisateur, string motDePasse) => Task.FromResult(false);
    }
}
