using CoupDeSonde.Api.Data;
using CoupDeSonde.Api.Models;

namespace CoupDeSonde.Api.Services;

public enum SubmissionStatus
{
    Succes,
    SondageIntrouvable,
    ParticipantDejaRepondu,
    ReponseInvalide
}

public record SubmissionResult(SubmissionStatus Status, string? Message = null);

public interface ISondageService
{
    Task<IReadOnlyList<SondageResume>> ListerSondagesAsync();
    Task<Sondage?> ObtenirSondageAsync(int id);
    Task<SubmissionResult> SoumettreReponseAsync(int sondageId, string participantId, ReponseSoumission soumission);
    Task<ResultatsSondage?> ObtenirResultatsAsync(int sondageId);
}

public class SondageService : ISondageService
{
    private readonly FileSondageRepository _repository;

    public SondageService(FileSondageRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<SondageResume>> ListerSondagesAsync()
    {
        var sondages = await _repository.GetAllAsync();
        return sondages.Select(s => new SondageResume(s.Id, s.Titre)).ToList();
    }

    public Task<Sondage?> ObtenirSondageAsync(int id) => _repository.GetByIdAsync(id);

    public async Task<SubmissionResult> SoumettreReponseAsync(int sondageId, string participantId, ReponseSoumission soumission)
    {
        var sondage = await _repository.GetByIdAsync(sondageId);
        if (sondage is null)
        {
            return new SubmissionResult(SubmissionStatus.SondageIntrouvable, $"Le sondage {sondageId} n'existe pas.");
        }

        if (!ReponsesValides(sondage, soumission))
        {
            return new SubmissionResult(SubmissionStatus.ReponseInvalide, "Une ou plusieurs reponses sont invalides pour ce sondage.");
        }

        var enregistre = await _repository.EnregistrerReponseSiNouveauAsync(sondageId, new ReponseEnregistree
        {
            ParticipantId = participantId,
            Reponses = soumission.Reponses,
            SoumisLe = DateTimeOffset.UtcNow
        });

        if (!enregistre)
        {
            return new SubmissionResult(SubmissionStatus.ParticipantDejaRepondu, "Ce participant a deja repondu a ce sondage.");
        }

        return new SubmissionResult(SubmissionStatus.Succes);
    }

    public async Task<ResultatsSondage?> ObtenirResultatsAsync(int sondageId)
    {
        var sondage = await _repository.GetByIdAsync(sondageId);
        if (sondage is null)
        {
            return null;
        }

        var reponses = await _repository.GetReponsesAsync(sondageId);

        var resultats = new ResultatsSondage
        {
            SondageId = sondageId,
            NombreParticipants = reponses.Count
        };

        foreach (var question in sondage.Questions)
        {
            var comptes = question.Choix.Keys.ToDictionary(choix => choix, _ => 0);

            foreach (var reponse in reponses)
            {
                if (reponse.Reponses.TryGetValue(question.Id, out var choix) && comptes.ContainsKey(choix))
                {
                    comptes[choix]++;
                }
            }

            resultats.Questions.Add(new ResultatQuestion
            {
                QuestionId = question.Id,
                Texte = question.Texte,
                Comptes = comptes
            });
        }

        return resultats;
    }

    // Plafond generaux : aucun sondage reel n'a plus de questions que ca. Rejette tot une
    // soumission anormalement volumineuse plutot que de la valider question par question.
    private const int NombreMaximalDeReponses = 50;

    private static bool ReponsesValides(Sondage sondage, ReponseSoumission soumission)
    {
        if (soumission.Reponses.Count is 0 or > NombreMaximalDeReponses)
        {
            return false;
        }

        foreach (var (questionId, choix) in soumission.Reponses)
        {
            var question = sondage.Questions.FirstOrDefault(q => q.Id == questionId);
            if (question is null || !question.Choix.ContainsKey(choix))
            {
                return false;
            }
        }

        return true;
    }
}
