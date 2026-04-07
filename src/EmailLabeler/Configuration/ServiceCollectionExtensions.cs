namespace EmailLabeler.Configuration;

using Microsoft.Extensions.Options;

/// <summary>Extension methods for configuring rules in the DI container.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers <see cref="RulesConfig"/> from the provided configuration with startup validation.</summary>
    public static IServiceCollection AddRulesConfig(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RulesConfig>(configuration);
        services.AddSingleton<IValidateOptions<RulesConfig>, RulesConfigValidator>();
        services.AddOptionsWithValidateOnStart<RulesConfig>();
        return services;
    }
}
