using System.Security.Claims;
using CoupDeSonde.Api.Models;
using CoupDeSonde.Api.Security;
using CoupDeSonde.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CoupDeSonde.Api.Controllers;

[ApiController]
[Route("api/comptes")]
[Authorize] // Cle d'API requise (scheme par defaut) pour toutes les actions de ce controleur.
public class ComptesController : ControllerBase
{
    private readonly IParticipantAuthService _authService;

    public ComptesController(IParticipantAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Cree un compte participant (nom d'utilisateur + mot de passe).</summary>
    [HttpPost("inscription")]
    public async Task<IActionResult> Inscription([FromBody] InscriptionRequete requete)
    {
        var resultat = await _authService.InscrireAsync(requete.NomUtilisateur, requete.MotDePasse);

        return resultat.Status switch
        {
            RegistrationStatus.Succes => Created(string.Empty, null),
            RegistrationStatus.NomUtilisateurDejaPris => Conflict(resultat.Message),
            RegistrationStatus.IdentifiantsInvalides => BadRequest(resultat.Message),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>Authentifie un participant et emet un cookie de session (UUID, expiration glissante).</summary>
    [HttpPost("connexion")]
    [EnableRateLimiting("connexion")]
    public async Task<IActionResult> Connexion([FromBody] ConnexionRequete requete)
    {
        var valide = await _authService.ValiderIdentifiantsAsync(requete.NomUtilisateur, requete.MotDePasse);
        if (!valide)
        {
            return Unauthorized("Nom d'utilisateur ou mot de passe invalide.");
        }

        var identite = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Name, requete.NomUtilisateur),
                new Claim("SessionId", Guid.NewGuid().ToString())
            },
            ParticipantCookieDefaults.Scheme);

        await HttpContext.SignInAsync(ParticipantCookieDefaults.Scheme, new ClaimsPrincipal(identite));

        return Ok();
    }

    /// <summary>Invalide la session du participant courant.</summary>
    [HttpPost("deconnexion")]
    [Authorize(AuthenticationSchemes = ParticipantCookieDefaults.Scheme)]
    public async Task<IActionResult> Deconnexion()
    {
        await HttpContext.SignOutAsync(ParticipantCookieDefaults.Scheme);
        return Ok();
    }
}
