using CoupDeSonde.Api.Data;
using CoupDeSonde.Api.Models;

namespace CoupDeSonde.Tests;

public class FileParticipantRepositoryTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "coupdesonde-participants-tests-" + Guid.NewGuid());
    private readonly FileParticipantRepository _repository;

    public FileParticipantRepositoryTests()
    {
        _repository = new FileParticipantRepository(_dataDir);
    }

    [Fact]
    public async Task GetByUsernameAsync_AucunFichier_RetourneNull()
    {
        var participant = await _repository.GetByUsernameAsync("inexistant");

        Assert.Null(participant);
    }

    [Fact]
    public async Task CreateAsync_NouveauNomUtilisateur_RetourneTrueEtPersiste()
    {
        var cree = await _repository.CreateAsync(new Participant { NomUtilisateur = "alice", MotDePasseHache = "hash" });
        var relu = await _repository.GetByUsernameAsync("alice");

        Assert.True(cree);
        Assert.NotNull(relu);
    }

    [Fact]
    public async Task CreateAsync_NomUtilisateurDejaPris_RetourneFalse()
    {
        await _repository.CreateAsync(new Participant { NomUtilisateur = "bob", MotDePasseHache = "hash1" });

        var deuxiemeCreation = await _repository.CreateAsync(new Participant { NomUtilisateur = "bob", MotDePasseHache = "hash2" });

        Assert.False(deuxiemeCreation);
    }

    [Fact]
    public async Task GetByUsernameAsync_FichierContientNull_RetourneNull()
    {
        Directory.CreateDirectory(_dataDir);
        await File.WriteAllTextAsync(Path.Combine(_dataDir, "participants.json"), "null");

        var participant = await _repository.GetByUsernameAsync("peu-importe");

        Assert.Null(participant);
    }

    [Fact]
    public async Task GetByUsernameAsync_EstInsensibleALaCasse()
    {
        await _repository.CreateAsync(new Participant { NomUtilisateur = "Charlie", MotDePasseHache = "hash" });

        var relu = await _repository.GetByUsernameAsync("charlie");

        Assert.NotNull(relu);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDir))
        {
            Directory.Delete(_dataDir, recursive: true);
        }
    }
}
