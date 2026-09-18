using CoupDeSonde.Api.Models;
using CoupDeSonde.Api.Security;
using CoupDeSonde.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoupDeSonde.Api.Controllers;

[ApiController]
[Route("api/sondages")]
[Authorize]
public class SondagesController : ControllerBase
{
    private readonly ISondageService _sondageService;
    private readonly IValidator<ReponseSoumission> _reponseValidator;

    public SondagesController(ISondageService sondageService, IValidator<ReponseSoumission> reponseValidator)
    {
        _sondageService = sondageService;
        _reponseValidator = reponseValidator;
    }

    /// <summary>Liste les sondages disponibles.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SondageResume>>> ObtenirTous()
    {
        return Ok(await _sondageService.ListerSondagesAsync());
    }

    /// <summary>Retourne les questions d'un sondage.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Sondage>> ObtenirParId(int id)
    {
        var sondage = await _sondageService.ObtenirSondageAsync(id);
        return sondage is null ? NotFound() : Ok(sondage);
    }

    /// <summary>
    /// Soumet les reponses d'un participant pour un sondage (une seule fois par participant).
    /// Necessite en plus une session participant authentifiee (cookie) : l'identite provient
    /// de la session, jamais d'un champ fourni par le client.
    /// </summary>
    [HttpPost("{id:int}/reponses")]
    [Authorize(AuthenticationSchemes = ParticipantCookieDefaults.Scheme)]
    public async Task<IActionResult> SoumettreReponse(int id, [FromBody] ReponseSoumission soumission)
    {
        var validation = await _reponseValidator.ValidateAsync(soumission);
        if (!validation.IsValid)
        {
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));
        }

        var participantId = User.Identity!.Name!;
        var resultat = await _sondageService.SoumettreReponseAsync(id, participantId, soumission);

        return resultat.Status switch
        {
            SubmissionStatus.Succes => Created($"api/sondages/{id}/resultats", null),
            SubmissionStatus.SondageIntrouvable => NotFound(resultat.Message),
            SubmissionStatus.ParticipantDejaRepondu => Conflict(resultat.Message),
            SubmissionStatus.ReponseInvalide => BadRequest(resultat.Message),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>Retourne les resultats agreges d'un sondage.</summary>
    [HttpGet("{id:int}/resultats")]
    public async Task<ActionResult<ResultatsSondage>> ObtenirResultats(int id)
    {
        var resultats = await _sondageService.ObtenirResultatsAsync(id);
        return resultats is null ? NotFound() : Ok(resultats);
    }
}
