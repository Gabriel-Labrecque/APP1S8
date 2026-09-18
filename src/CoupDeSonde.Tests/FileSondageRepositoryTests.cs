using CoupDeSonde.Api.Data;
using CoupDeSonde.Api.Models;

namespace CoupDeSonde.Tests;

/// <summary>Tests unitaires directs sur la couche de persistance, isoles du pipeline HTTP.</summary>
public class FileSondageRepositoryTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "coupdesonde-repo-tests-" + Guid.NewGuid());
    private readonly FileSondageRepository _repository;

    public FileSondageRepositoryTests()
    {
        _repository = new FileSondageRepository(_dataDir);
    }

    [Fact]
    public async Task GetReponsesAsync_FichierContientNull_RetourneListeVide()
    {
        var chemin = Path.Combine(_dataDir, "reponses", "42.json");
        await File.WriteAllTextAsync(chemin, "null");

        var reponses = await _repository.GetReponsesAsync(42);

        Assert.Empty(reponses);
    }

    [Fact]
    public async Task GetReponsesAsync_AucunFichier_RetourneListeVide()
    {
        var reponses = await _repository.GetReponsesAsync(999);

        Assert.Empty(reponses);
    }

    [Fact]
    public async Task EnregistrerReponseSiNouveauAsync_PremiereFois_RetourneTrueEtPersiste()
    {
        var reponse = new ReponseEnregistree
        {
            ParticipantId = "p1",
            Reponses = new Dictionary<int, string> { [1] = "a" },
            SoumisLe = DateTimeOffset.UtcNow
        };

        var enregistre = await _repository.EnregistrerReponseSiNouveauAsync(1, reponse);
        var reponses = await _repository.GetReponsesAsync(1);

        Assert.True(enregistre);
        Assert.Single(reponses);
        Assert.Equal("p1", reponses[0].ParticipantId);
    }

    [Fact]
    public async Task EnregistrerReponseSiNouveauAsync_ParticipantDejaEnregistre_RetourneFalse()
    {
        var reponse = new ReponseEnregistree
        {
            ParticipantId = "p1",
            Reponses = new Dictionary<int, string> { [1] = "a" },
            SoumisLe = DateTimeOffset.UtcNow
        };
        await _repository.EnregistrerReponseSiNouveauAsync(1, reponse);

        var deuxiemeTentative = await _repository.EnregistrerReponseSiNouveauAsync(1, reponse);
        var reponses = await _repository.GetReponsesAsync(1);

        Assert.False(deuxiemeTentative);
        Assert.Single(reponses);
    }

    [Fact]
    public async Task EnregistrerReponseSiNouveauAsync_AppelsSimultanesMemeParticipant_UneSeuleReussit()
    {
        var reponse = new ReponseEnregistree
        {
            ParticipantId = "p-concurrent",
            Reponses = new Dictionary<int, string> { [1] = "a" },
            SoumisLe = DateTimeOffset.UtcNow
        };

        var resultats = await Task.WhenAll(
            _repository.EnregistrerReponseSiNouveauAsync(1, reponse),
            _repository.EnregistrerReponseSiNouveauAsync(1, reponse));

        Assert.Equal(1, resultats.Count(r => r));
        var reponses = await _repository.GetReponsesAsync(1);
        Assert.Single(reponses);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDir))
        {
            Directory.Delete(_dataDir, recursive: true);
        }
    }
}
