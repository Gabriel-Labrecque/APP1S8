using CoupDeSonde.Api.Models;
using FluentValidation;

namespace CoupDeSonde.Api.Validators;

public class InscriptionRequeteValidator : AbstractValidator<InscriptionRequete>
{
    public InscriptionRequeteValidator()
    {
        RuleFor(x => x.NomUtilisateur)
            .NotEmpty()
            .Length(1, 50)
            .Matches(@"^[a-zA-Z0-9_.-]+$")
            .WithMessage("Le nom d'utilisateur ne peut contenir que des lettres, chiffres, '_', '-' ou '.'.");

        RuleFor(x => x.MotDePasse)
            .NotEmpty()
            .Length(8, 200);
    }
}
