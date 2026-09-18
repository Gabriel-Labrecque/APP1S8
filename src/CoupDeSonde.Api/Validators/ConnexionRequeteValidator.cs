using CoupDeSonde.Api.Models;
using FluentValidation;

namespace CoupDeSonde.Api.Validators;

public class ConnexionRequeteValidator : AbstractValidator<ConnexionRequete>
{
    public ConnexionRequeteValidator()
    {
        RuleFor(x => x.NomUtilisateur)
            .NotEmpty()
            .Length(1, 50)
            .Matches(@"^[a-zA-Z0-9_.-]+$");

        RuleFor(x => x.MotDePasse)
            .NotEmpty()
            .Length(1, 200);
    }
}
