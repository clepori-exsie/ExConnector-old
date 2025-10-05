using ExConnector.Core.DTOs;
using FluentValidation;

namespace ExConnector.Core.Validators;

/// <summary>
/// Validateur pour TiersDto
/// </summary>
public class TiersDtoValidator : AbstractValidator<TiersDto>
{
    public TiersDtoValidator()
    {
        RuleFor(x => x.Numero)
            .NotEmpty().WithMessage("Le numéro de tiers est obligatoire")
            .MaximumLength(17).WithMessage("Le numéro de tiers ne peut pas dépasser 17 caractères");

        RuleFor(x => x.Intitule)
            .MaximumLength(69).WithMessage("L'intitulé ne peut pas dépasser 69 caractères")
            .When(x => !string.IsNullOrEmpty(x.Intitule));
    }
}

