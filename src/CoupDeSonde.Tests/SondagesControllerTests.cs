using System.Net;
using System.Net.Http.Json;
using CoupDeSonde.Api.Models;

namespace CoupDeSonde.Tests;

public class SondagesControllerTests : IClassFixture<SondageApiFactory>
{
    private readonly SondageApiFactory _factory;

    public SondagesControllerTests(SondageApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    private HttpClient CreateAuthorizedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", SondageApiFactory.ValidApiKey);
        return client;
    }

    [Fact]
    public async Task GetSondages_SansCleApi_Retourne401()
    {
        var response = await CreateClient().GetAsync("/api/sondages");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSondages_CleApiInvalide_Retourne401()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "cle-invalide");

        var response = await client.GetAsync("/api/sondages");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSondages_CleApiValide_RetourneLesDeuxSondages()
    {
        var response = await CreateAuthorizedClient().GetAsync("/api/sondages");

        response.EnsureSuccessStatusCode();
        var sondages = await response.Content.ReadFromJsonAsync<List<SondageResume>>();
        Assert.Equal(2, sondages!.Count);
    }

    [Fact]
    public async Task GetSondageParId_IdInexistant_Retourne404()
    {
        var response = await CreateAuthorizedClient().GetAsync("/api/sondages/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSondageParId_IdValide_RetourneLeSondage()
    {
        var response = await CreateAuthorizedClient().GetAsync("/api/sondages/1");

        response.EnsureSuccessStatusCode();
        var sondage = await response.Content.ReadFromJsonAsync<Sondage>();
        Assert.Equal(1, sondage!.Id);
        Assert.Equal(2, sondage.Questions.Count);
    }

    [Fact]
    public async Task PostReponse_PremiereSoumission_Retourne201()
    {
        var client = await _factory.CreateParticipantClientAsync();
        var soumission = new ReponseSoumission { Reponses = new Dictionary<int, string> { [1] = "a", [2] = "b" } };

        var response = await client.PostAsJsonAsync("/api/sondages/1/reponses", soumission);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostReponse_ParticipantDejaRepondu_Retourne409()
    {
        var client = await _factory.CreateParticipantClientAsync();
        var soumission = new ReponseSoumission { Reponses = new Dictionary<int, string> { [1] = "a", [2] = "b" } };

        await client.PostAsJsonAsync("/api/sondages/1/reponses", soumission);
        var deuxiemeTentative = await client.PostAsJsonAsync("/api/sondages/1/reponses", soumission);

        Assert.Equal(HttpStatusCode.Conflict, deuxiemeTentative.StatusCode);
    }

    [Fact]
    public async Task PostReponse_SondageInexistant_Retourne404()
    {
        var client = await _factory.CreateParticipantClientAsync();
        var soumission = new ReponseSoumission { Reponses = new Dictionary<int, string> { [1] = "a" } };

        var response = await client.PostAsJsonAsync("/api/sondages/999/reponses", soumission);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostReponse_QuestionIdInexistante_Retourne400()
    {
        var client = await _factory.CreateParticipantClientAsync();
        var soumission = new ReponseSoumission { Reponses = new Dictionary<int, string> { [999] = "a" } };

        var response = await client.PostAsJsonAsync("/api/sondages/1/reponses", soumission);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetResultats_SoumissionPartielle_NeCompteQueLaQuestionRepondue()
    {
        var participant = await _factory.CreateParticipantClientAsync();
        var soumission = new ReponseSoumission
        {
            Reponses = new Dictionary<int, string> { [1] = "a" } // question 2 volontairement omise
        };
        await participant.PostAsJsonAsync("/api/sondages/1/reponses", soumission);

        var response = await CreateAuthorizedClient().GetAsync("/api/sondages/1/resultats");

        response.EnsureSuccessStatusCode();
        var resultats = await response.Content.ReadFromJsonAsync<ResultatsSondage>();
        var question2 = resultats!.Questions.Single(q => q.QuestionId == 2);
        Assert.All(question2.Comptes.Values, compte => Assert.True(compte >= 0));
    }

    [Fact]
    public async Task PostReponse_ValeurVide_Retourne400()
    {
        var client = await _factory.CreateParticipantClientAsync();
        var soumission = new ReponseSoumission { Reponses = new Dictionary<int, string> { [1] = "" } };

        var response = await client.PostAsJsonAsync("/api/sondages/1/reponses", soumission);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostReponse_ValeurTropLongue_Retourne400()
    {
        var client = await _factory.CreateParticipantClientAsync();
        var soumission = new ReponseSoumission { Reponses = new Dictionary<int, string> { [1] = new string('a', 21) } };

        var response = await client.PostAsJsonAsync("/api/sondages/1/reponses", soumission);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostReponse_ChoixInvalide_Retourne400()
    {
        var client = await _factory.CreateParticipantClientAsync();
        var soumission = new ReponseSoumission { Reponses = new Dictionary<int, string> { [1] = "z" } };

        var response = await client.PostAsJsonAsync("/api/sondages/1/reponses", soumission);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostReponse_AucuneReponse_Retourne400()
    {
        var client = await _factory.CreateParticipantClientAsync();
        var soumission = new ReponseSoumission { Reponses = new Dictionary<int, string>() };

        var response = await client.PostAsJsonAsync("/api/sondages/1/reponses", soumission);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostReponse_SansCleApi_Retourne401()
    {
        var soumission = new ReponseSoumission { Reponses = new Dictionary<int, string> { [1] = "a" } };

        var response = await CreateClient().PostAsJsonAsync("/api/sondages/1/reponses", soumission);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostReponse_SansSessionParticipant_Retourne401()
    {
        // Cle d'API valide, mais aucune session participant (pas de connexion prealable).
        var soumission = new ReponseSoumission { Reponses = new Dictionary<int, string> { [1] = "a" } };

        var response = await CreateAuthorizedClient().PostAsJsonAsync("/api/sondages/1/reponses", soumission);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetResultats_ApresSoumission_ComptabiliseLesReponses()
    {
        var participant = await _factory.CreateParticipantClientAsync();
        var soumission = new ReponseSoumission { Reponses = new Dictionary<int, string> { [1] = "a", [2] = "b" } };
        await participant.PostAsJsonAsync("/api/sondages/2/reponses", soumission);

        var response = await CreateAuthorizedClient().GetAsync("/api/sondages/2/resultats");

        response.EnsureSuccessStatusCode();
        var resultats = await response.Content.ReadFromJsonAsync<ResultatsSondage>();
        Assert.True(resultats!.NombreParticipants >= 1);
        Assert.Equal(2, resultats.SondageId);
    }

    [Fact]
    public async Task GetResultats_SondageInexistant_Retourne404()
    {
        var response = await CreateAuthorizedClient().GetAsync("/api/sondages/999/resultats");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
