using System.Net.Http.Json;
using System.Text.Json;
using CoupDeSonde.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CoupDeSonde.Tests;

/// <summary>
/// Environnement de test isole : cle d'API et repertoire de donnees dedies,
/// injectes uniquement dans le processus de test (jamais dans l'application livree).
/// </summary>
public class SondageApiFactory : WebApplicationFactory<Program>
{
    public const string ValidApiKey = "test-only-key-never-shipped";

    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "coupdesonde-tests-" + Guid.NewGuid());

    public SondageApiFactory()
    {
        // Le cookie participant est marque Secure (voir Program.cs) : sans base address https,
        // HttpClient refuse silencieusement de le renvoyer sur les requetes suivantes.
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        SeedData();

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiKeys:0"] = ValidApiKey,
                ["DataDirectory"] = _dataDirectory,
                // Le reste de la suite se connecte a repetition ; seul le test dedie au
                // rate limiting doit voir un seuil bas (voir EdgeCaseTests).
                ["RateLimiting:ConnexionPermitLimit"] = "1000"
            });
        });
    }

    private void SeedData()
    {
        var sondagesDir = Path.Combine(_dataDirectory, "sondages");
        Directory.CreateDirectory(sondagesDir);
        Directory.CreateDirectory(Path.Combine(_dataDirectory, "reponses"));

        var options = new JsonSerializerOptions { WriteIndented = true };

        for (var id = 1; id <= 2; id++)
        {
            var sondage = new Sondage
            {
                Id = id,
                Titre = $"Sondage de test {id}",
                Questions = new List<Question>
                {
                    new()
                    {
                        Id = 1,
                        Texte = "Question 1?",
                        Choix = new Dictionary<string, string> { ["a"] = "Choix A", ["b"] = "Choix B" }
                    },
                    new()
                    {
                        Id = 2,
                        Texte = "Question 2?",
                        Choix = new Dictionary<string, string> { ["a"] = "Choix A", ["b"] = "Choix B" }
                    }
                }
            };

            File.WriteAllText(
                Path.Combine(sondagesDir, $"{id}.json"),
                JsonSerializer.Serialize(sondage, options));
        }
    }

    /// <summary>
    /// Cree un client HTTP muni d'une cle d'API valide, inscrit un nouveau participant
    /// et ouvre sa session (cookie) : pret a soumettre des reponses a un sondage.
    /// </summary>
    public async Task<HttpClient> CreateParticipantClientAsync(
        string? nomUtilisateur = null,
        string motDePasse = "MotDePasse123!")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ValidApiKey);
        nomUtilisateur ??= "participant-" + Guid.NewGuid().ToString("N")[..8];

        await client.PostAsJsonAsync("/api/comptes/inscription", new InscriptionRequete(nomUtilisateur, motDePasse));
        var connexion = await client.PostAsJsonAsync("/api/comptes/connexion", new ConnexionRequete(nomUtilisateur, motDePasse));
        connexion.EnsureSuccessStatusCode();

        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_dataDirectory))
        {
            try
            {
                Directory.Delete(_dataDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Nettoyage best-effort ; ne doit pas faire echouer les tests.
            }
        }
    }
}
