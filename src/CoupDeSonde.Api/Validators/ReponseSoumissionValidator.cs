using CoupDeSonde.Api.Models;
using FluentValidation;

namespace CoupDeSonde.Api.Validators;

public class ReponseSoumissionValidator : AbstractValidator<ReponseSoumission>
{
    public ReponseSoumissionValidator()
    {
        RuleFor(x => x.Reponses)
            .Must(r => r.Count is > 0 and <= 50)
            .WithMessage("Le nombre de reponses doit etre entre 1 et 50.")
            .Must(r => r.Values.All(v => !string.IsNullOrEmpty(v) && v.Length <= 20))
            .WithMessage("Chaque reponse doit contenir entre 1 et 20 caracteres.");
    }
}
