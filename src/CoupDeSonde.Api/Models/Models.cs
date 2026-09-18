using System.ComponentModel.DataAnnotations;

namespace CoupDeSonde.Api.Models;

public class Question
{
    public int Id { get; set; }
    public string Texte { get; set; } = string.Empty;
    public Dictionary<string, string> Choix { get; set; } = new();
}

public class Sondage
{
    public int Id { get; set; }
    public string Titre { get; set; } = string.Empty;
    public List<Question> Questions { get; set; } = new();
}

public record SondageResume(int Id, string Titre);

public class ReponseSoumission
{
    // L'identite du participant provient de la session authentifiee (cookie), jamais du corps de la requete.
    // Cle = Id de la question, Valeur = clef du choix retenu (ex. "a")
    public Dictionary<int, string> Reponses { get; set; } = new();
}

public class ReponseEnregistree
{
    public string ParticipantId { get; set; } = string.Empty;
    public Dictionary<int, string> Reponses { get; set; } = new();
    public DateTimeOffset SoumisLe { get; set; }
}

public class ResultatQuestion
{
    public int QuestionId { get; set; }
    public string Texte { get; set; } = string.Empty;

    // Cle = clef du choix (ex. "a"), Valeur = nombre de participants l'ayant choisi
    public Dictionary<string, int> Comptes { get; set; } = new();
}

public class ResultatsSondage
{
    public int SondageId { get; set; }
    public int NombreParticipants { get; set; }
    public List<ResultatQuestion> Questions { get; set; } = new();
}

public class Participant
{
    public string NomUtilisateur { get; set; } = string.Empty;
    public string MotDePasseHache { get; set; } = string.Empty;
}

public record InscriptionRequete(
    [Required, StringLength(50, MinimumLength = 1)] string NomUtilisateur,
    [Required, StringLength(200, MinimumLength = 8)] string MotDePasse);

public record ConnexionRequete(
    [Required, StringLength(50, MinimumLength = 1)] string NomUtilisateur,
    [Required, StringLength(200, MinimumLength = 1)] string MotDePasse);
