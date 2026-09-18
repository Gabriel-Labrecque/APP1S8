using CoupDeSonde.Api.Data;
using CoupDeSonde.Api.Models;
using CoupDeSonde.Api.Services;
using Microsoft.AspNetCore.Identity;

namespace CoupDeSonde.Tests;

/// <summary>
/// Tests unitaires directs sur ParticipantAuthService, sans passer par le pipeline HTTP.
/// Ceci exerce la validation "defense en profondeur" du service meme quand un appelant
/// (autre que le controleur, qui filtre deja via les DataAnnotations) ne validerait pas en amont.
/// </summary>
public class ParticipantAuthServiceTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "coupdesonde-authservice-tests-" + Guid.NewGuid());
    private readonly ParticipantAuthService _service;

    public ParticipantAuthServiceTests()
    {
        var repository = new FileParticipantRepository(_dataDir);
        _service = new ParticipantAuthService(repository, new PasswordHasher<Participant>());
    }

    [Fact]
    public async Task InscrireAsync_NomUtilisateurVide_RetourneIdentifiantsInvalides()
    {
        var resultat = await _service.InscrireAsync("", "MotDePasse123!");

        Assert.Equal(RegistrationStatus.IdentifiantsInvalides, resultat.Status);
    }

    [Fact]
    public async Task InscrireAsync_MotDePasseTropCourt_RetourneIdentifiantsInvalides()
    {
        var resultat = await _service.InscrireAsync("utilisateur", "court");

        Assert.Equal(RegistrationStatus.IdentifiantsInvalides, resultat.Status);
    }

    [Fact]
    public async Task InscrireAsync_IdentifiantsValides_RetourneSucces()
    {
        var resultat = await _service.InscrireAsync("nouvel-utilisateur", "MotDePasse123!");

        Assert.Equal(RegistrationStatus.Succes, resultat.Status);
    }

    [Fact]
    public async Task ValiderIdentifiantsAsync_UtilisateurInexistant_RetourneFalse()
    {
        var valide = await _service.ValiderIdentifiantsAsync("inexistant", "peu-importe");

        Assert.False(valide);
    }

    [Fact]
    public async Task ValiderIdentifiantsAsync_MotDePasseIncorrect_RetourneFalse()
    {
        await _service.InscrireAsync("bob", "MotDePasse123!");

        var valide = await _service.ValiderIdentifiantsAsync("bob", "MauvaisMotDePasse");

        Assert.False(valide);
    }

    [Fact]
    public async Task ValiderIdentifiantsAsync_IdentifiantsCorrects_RetourneTrue()
    {
        await _service.InscrireAsync("alice", "MotDePasse123!");

        var valide = await _service.ValiderIdentifiantsAsync("alice", "MotDePasse123!");

        Assert.True(valide);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDir))
        {
            Directory.Delete(_dataDir, recursive: true);
        }
    }
}
