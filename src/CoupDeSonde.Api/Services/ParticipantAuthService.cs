using CoupDeSonde.Api.Data;
using CoupDeSonde.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace CoupDeSonde.Api.Services;

public enum RegistrationStatus
{
    Succes,
    NomUtilisateurDejaPris,
    IdentifiantsInvalides
}

public record RegistrationResult(RegistrationStatus Status, string? Message = null);

public interface IParticipantAuthService
{
    Task<RegistrationResult> InscrireAsync(string nomUtilisateur, string motDePasse);
    Task<bool> ValiderIdentifiantsAsync(string nomUtilisateur, string motDePasse);
}

public class ParticipantAuthService : IParticipantAuthService
{
    private const int LongueurMinimaleMotDePasse = 8;

    // Utilise pour un hachage "factice" quand le nom d'utilisateur n'existe pas, afin que
    // ValiderIdentifiantsAsync prenne un temps comparable dans les deux cas et ne revele pas,
    // par le seul temps de reponse, quels noms d'utilisateur existent (canal auxiliaire).
    private static readonly Participant ParticipantFactice = new() { NomUtilisateur = "_factice_" };

    private readonly FileParticipantRepository _repository;
    private readonly IPasswordHasher<Participant> _hasher;
    private readonly string _hacheFactice;

    public ParticipantAuthService(FileParticipantRepository repository, IPasswordHasher<Participant> hasher)
    {
        _repository = repository;
        _hasher = hasher;
        _hacheFactice = _hasher.HashPassword(ParticipantFactice, "mot-de-passe-factice-pour-egaliser-le-temps");
    }

    public async Task<RegistrationResult> InscrireAsync(string nomUtilisateur, string motDePasse)
    {
        if (string.IsNullOrWhiteSpace(nomUtilisateur) || string.IsNullOrWhiteSpace(motDePasse)
            || motDePasse.Length < LongueurMinimaleMotDePasse)
        {
            return new RegistrationResult(
                RegistrationStatus.IdentifiantsInvalides,
                $"Le nom d'utilisateur est requis et le mot de passe doit contenir au moins {LongueurMinimaleMotDePasse} caracteres.");
        }

        var participant = new Participant { NomUtilisateur = nomUtilisateur.Trim() };
        participant.MotDePasseHache = _hasher.HashPassword(participant, motDePasse);

        var cree = await _repository.CreateAsync(participant);

        return cree
            ? new RegistrationResult(RegistrationStatus.Succes)
            : new RegistrationResult(RegistrationStatus.NomUtilisateurDejaPris, "Ce nom d'utilisateur est deja pris.");
    }

    public async Task<bool> ValiderIdentifiantsAsync(string nomUtilisateur, string motDePasse)
    {
        var participant = await _repository.GetByUsernameAsync(nomUtilisateur);

        if (participant is null)
        {
            // Verification factice : paie le meme cout de hachage que le cas "utilisateur existant"
            // pour qu'un attaquant ne puisse pas deduire l'existence d'un compte via le temps de reponse.
            _hasher.VerifyHashedPassword(ParticipantFactice, _hacheFactice, motDePasse);
            return false;
        }

        var resultat = _hasher.VerifyHashedPassword(participant, participant.MotDePasseHache, motDePasse);
        return resultat is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
