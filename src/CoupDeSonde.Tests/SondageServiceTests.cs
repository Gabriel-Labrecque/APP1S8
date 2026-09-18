using System.Text.Json;
using CoupDeSonde.Api.Data;
using CoupDeSonde.Api.Models;
using CoupDeSonde.Api.Services;

namespace CoupDeSonde.Tests;

/// <summary>
/// Tests unitaires directs sur SondageService, sans passer par le pipeline HTTP. Exerce la
/// validation "defense en profondeur" du service (nombre de reponses) meme quand FluentValidation,
/// au niveau du controleur, intercepte deja la plupart de ces cas en pratique.
/// </summary>
public class SondageServiceTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "coupdesonde-sondageservice-tests-" + Guid.NewGuid());
    private readonly SondageService _service;

    public SondageServiceTests()
    {
        var repository = new FileSondageRepository(_dataDir);
        _service = new SondageService(repository);

        var sondage = new Sondage
        {
            Id = 1,
            Titre = "Sondage de test",
            Questions = new List<Question>
            {
                new() { Id = 1, Texte = "Question 1?", Choix = new() { ["a"] = "Choix A", ["b"] = "Choix B" } }
            }
        };
        var sondagesDir = Path.Combine(_dataDir, "sondages");
        Directory.CreateDirectory(sondagesDir);
        File.WriteAllText(Path.Combine(sondagesDir, "1.json"), JsonSerializer.Serialize(sondage));
    }

    [Fact]
    public async Task SoumettreReponseAsync_AucuneReponse_RetourneReponseInvalide()
    {
        var soumission = new ReponseSoumission { Reponses = new Dictionary<int, string>() };

        var resultat = await _service.SoumettreReponseAsync(1, "participant", soumission);

        Assert.Equal(SubmissionStatus.ReponseInvalide, resultat.Status);
    }

    [Fact]
    public async Task SoumettreReponseAsync_TropDeReponses_RetourneReponseInvalide()
    {
        var reponses = Enumerable.Range(1, 51).ToDictionary(i => i, _ => "a");
        var soumission = new ReponseSoumission { Reponses = reponses };

        var resultat = await _service.SoumettreReponseAsync(1, "participant", soumission);

        Assert.Equal(SubmissionStatus.ReponseInvalide, resultat.Status);
    }

    [Fact]
    public async Task SoumettreReponseAsync_ReponseValide_RetourneSucces()
    {
        var soumission = new ReponseSoumission { Reponses = new Dictionary<int, string> { [1] = "a" } };

        var resultat = await _service.SoumettreReponseAsync(1, "participant", soumission);

        Assert.Equal(SubmissionStatus.Succes, resultat.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDir))
        {
            Directory.Delete(_dataDir, recursive: true);
        }
    }
}
