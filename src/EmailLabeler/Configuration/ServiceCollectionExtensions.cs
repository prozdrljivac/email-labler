namespace EmailLabeler.Configuration;

using EmailLabeler.Adapters;
using EmailLabeler.Ports;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Microsoft.Extensions.Options;

/// <summary>Extension methods for configuring services in the DI container.</summary>
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

    /// <summary>Registers Gmail integration services with startup validation of required environment variables.</summary>
    public static IServiceCollection AddGmailIntegration(this IServiceCollection services)
    {
        services.Configure<GmailConfig>(config =>
        {
            config.ClientId = Environment.GetEnvironmentVariable("GMAIL_CLIENT_ID") ?? "";
            config.ClientSecret = Environment.GetEnvironmentVariable("GMAIL_CLIENT_SECRET") ?? "";
            config.RefreshToken = Environment.GetEnvironmentVariable("GMAIL_REFRESH_TOKEN") ?? "";
            config.UserEmail = Environment.GetEnvironmentVariable("GMAIL_USER_EMAIL") ?? "";
            config.TopicName = Environment.GetEnvironmentVariable("PUBSUB_TOPIC_NAME") ?? "";
        });
        services.AddSingleton<IValidateOptions<GmailConfig>, GmailConfigValidator>();
        services.AddOptionsWithValidateOnStart<GmailConfig>();

        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IOptions<GmailConfig>>().Value;
            var credential = new UserCredential(
                new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = config.ClientId,
                        ClientSecret = config.ClientSecret
                    }
                }),
                config.UserEmail,
                new TokenResponse { RefreshToken = config.RefreshToken });

            return new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "EmailLabeler"
            });
        });

        services.AddSingleton<IGmailRepository>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<GmailConfig>>().Value;
            return new GmailRepository(
                sp.GetRequiredService<GmailService>(),
                config.UserEmail,
                config.TopicName,
                sp.GetRequiredService<ILogger<GmailRepository>>());
        });
        services.AddSingleton<IEmailRepository>(sp => sp.GetRequiredService<IGmailRepository>());

        return services;
    }
}
