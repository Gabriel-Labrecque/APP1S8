using System.Net;
using System.Net.Http.Json;
using CoupDeSonde.Api.Models;

namespace CoupDeSonde.Tests;

public class ComptesControllerTests : IClassFixture<SondageApiFactory>
{
    private readonly SondageApiFactory _factory;

    public ComptesControllerTests(SondageApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthorizedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", SondageApiFactory.ValidApiKey);
        return client;
    }

    [Fact]
    public async Task Inscription_NouveauNomUtilisateur_Retourne201()
    {
        var nomUtilisateur = "participant-" + Guid.NewGuid().ToString("N")[..8];
        var requete = new InscriptionRequete(nomUtilisateur, "MotDePasse123!");

        var response = await CreateAuthorizedClient().PostAsJsonAsync("/api/comptes/inscription", requete);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Inscription_NomUtilisateurDejaPris_Retourne409()
    {
        var nomUtilisateur = "participant-" + Guid.NewGuid().ToString("N")[..8];
        var requete = new InscriptionRequete(nomUtilisateur, "MotDePasse123!");
        var client = CreateAuthorizedClient();
        await client.PostAsJsonAsync("/api/comptes/inscription", requete);

        var deuxiemeTentative = await client.PostAsJsonAsync("/api/comptes/inscription", requete);

        Assert.Equal(HttpStatusCode.Conflict, deuxiemeTentative.StatusCode);
    }

    [Fact]
    public async Task Inscription_NomUtilisateurTropLong_Retourne400()
    {
        var requete = new InscriptionRequete(new string('a', 51), "MotDePasse123!");

        var response = await CreateAuthorizedClient().PostAsJsonAsync("/api/comptes/inscription", requete);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("user*name")]
    [InlineData("user name")]
    [InlineData("admin'--")]
    [InlineData("аdmin")] // 'а' cyrillique, pas 'a' latin : homoglyphe
    public async Task Inscription_NomUtilisateurCaracteresInvalides_Retourne400(string nomUtilisateur)
    {
        var requete = new InscriptionRequete(nomUtilisateur, "MotDePasse123!");

        var response = await CreateAuthorizedClient().PostAsJsonAsync("/api/comptes/inscription", requete);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Inscription_MotDePasseTropCourt_Retourne400()
    {
        var requete = new InscriptionRequete("participant-" + Guid.NewGuid().ToString("N")[..8], "court");

        var response = await CreateAuthorizedClient().PostAsJsonAsync("/api/comptes/inscription", requete);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Inscription_NomUtilisateurVide_Retourne400()
    {
        var requete = new InscriptionRequete("", "MotDePasse123!");

        var response = await CreateAuthorizedClient().PostAsJsonAsync("/api/comptes/inscription", requete);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Inscription_MotDePasseVide_Retourne400()
    {
        var requete = new InscriptionRequete("participant-" + Guid.NewGuid().ToString("N")[..8], "");

        var response = await CreateAuthorizedClient().PostAsJsonAsync("/api/comptes/inscription", requete);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Inscription_SansCleApi_Retourne401()
    {
        var requete = new InscriptionRequete("peu-importe", "MotDePasse123!");

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/comptes/inscription", requete);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Connexion_FormatInvalide_Retourne400()
    {
        var requete = new ConnexionRequete("", "peu-importe");

        var response = await CreateAuthorizedClient().PostAsJsonAsync("/api/comptes/connexion", requete);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Connexion_IdentifiantsValides_Retourne200()
    {
        var nomUtilisateur = "participant-" + Guid.NewGuid().ToString("N")[..8];
        var client = CreateAuthorizedClient();
        await client.PostAsJsonAsync("/api/comptes/inscription", new InscriptionRequete(nomUtilisateur, "MotDePasse123!"));

        var response = await client.PostAsJsonAsync("/api/comptes/connexion", new ConnexionRequete(nomUtilisateur, "MotDePasse123!"));

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Connexion_MotDePasseInvalide_Retourne401()
    {
        var nomUtilisateur = "participant-" + Guid.NewGuid().ToString("N")[..8];
        var client = CreateAuthorizedClient();
        await client.PostAsJsonAsync("/api/comptes/inscription", new InscriptionRequete(nomUtilisateur, "MotDePasse123!"));

        var response = await client.PostAsJsonAsync("/api/comptes/connexion", new ConnexionRequete(nomUtilisateur, "MauvaisMotDePasse"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Connexion_UtilisateurInexistant_Retourne401()
    {
        var response = await CreateAuthorizedClient()
            .PostAsJsonAsync("/api/comptes/connexion", new ConnexionRequete("inexistant-" + Guid.NewGuid(), "MotDePasse123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Deconnexion_SansSession_Retourne401()
    {
        var response = await CreateAuthorizedClient().PostAsync("/api/comptes/deconnexion", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Deconnexion_AvecSession_Retourne200()
    {
        var client = await _factory.CreateParticipantClientAsync();

        var response = await client.PostAsync("/api/comptes/deconnexion", content: null);

        response.EnsureSuccessStatusCode();
    }
}
