using ExConnector.Models;
using FluentValidation;

namespace ExConnector.Core.Validators;

/// <summary>
/// Validateur pour SageConfig
/// </summary>
public class SageConfigValidator : AbstractValidator<SageConfig>
{
    public SageConfigValidator()
    {
        // Validation conditionnelle : soit MAEPath, soit CompanyServer+CompanyDatabaseName
        RuleFor(x => x)
            .Must(config => !string.IsNullOrWhiteSpace(config.MAEPath) ||
                           (!string.IsNullOrWhiteSpace(config.CompanyServer) && 
                            !string.IsNullOrWhiteSpace(config.CompanyDatabaseName)))
            .WithMessage("Vous devez fournir soit un chemin .MAE, soit un serveur SQL + nom de base de données");

        When(x => !string.IsNullOrWhiteSpace(x.MAEPath), () =>
        {
            RuleFor(x => x.MAEPath)
                .Must(path => path!.EndsWith(".mae", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Le chemin MAE doit pointer vers un fichier .mae");
        });

        When(x => !string.IsNullOrWhiteSpace(x.CompanyServer), () =>
        {
            RuleFor(x => x.CompanyServer)
                .MaximumLength(255).WithMessage("Le nom du serveur est trop long");

            RuleFor(x => x.CompanyDatabaseName)
                .NotEmpty().WithMessage("Le nom de la base de données est obligatoire si un serveur est spécifié")
                .MaximumLength(128).WithMessage("Le nom de la base de données est trop long");
        });

        RuleFor(x => x.UserName)
            .MaximumLength(50).WithMessage("Le nom d'utilisateur est trop long")
            .When(x => !string.IsNullOrWhiteSpace(x.UserName));

        RuleFor(x => x.Password)
            .MaximumLength(500).WithMessage("Le mot de passe est trop long (ou chiffrement corrompu)")
            .When(x => !string.IsNullOrWhiteSpace(x.Password));
    }
}

