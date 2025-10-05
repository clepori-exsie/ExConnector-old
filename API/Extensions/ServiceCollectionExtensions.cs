using ExConnector.Application.Services;
using ExConnector.Core.Interfaces;
using ExConnector.Infrastructure.Persistence;
using ExConnector.Infrastructure.Sage;
using ExConnector.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace ExConnector.API.Extensions;

/// <summary>
/// Extensions pour configurer l'injection de dépendances
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Enregistre tous les services de l'application
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Infrastructure - Sage
        services.AddSingleton<AutoInteropGenerator>();
        services.AddSingleton<ISageConnectionFactory, SageConnectionFactory>();
        services.AddSingleton<SageVersionDetector>();
        
        // Infrastructure - Security
        services.AddSingleton<IAdminTokenService, AdminTokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IDataProtectionService, DpapiDataProtectionService>();
        
        // Infrastructure - Persistence
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        
        // Application - Services
        services.AddSingleton<ISageService, SageService>();
        services.AddSingleton<ITiersService, TiersService>();

        return services;
    }
}

