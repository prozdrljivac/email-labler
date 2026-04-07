using EmailLabeler.Adapters;
using EmailLabeler.Ports;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmailLabeler.Integration.Tests.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public required string WireMockBaseUrl { get; init; }
    public string VerificationToken { get; init; } = "test-token";
    public Dictionary<string, string?> ExtraConfig { get; init; } = new();

    private static readonly string RepoRoot = FindRepoRoot();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(WebHostDefaults.ContentRootKey, RepoRoot);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["rules:0:match:from"] = "@newsletter.com",
                ["rules:0:actions:0:type"] = "label",
                ["rules:0:actions:0:label"] = "Newsletters",
                ["PUBSUB_VERIFICATION_TOKEN"] = VerificationToken,
            };
            foreach (var kvp in ExtraConfig)
                settings[kvp.Key] = kvp.Value;
            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            // Remove real Gmail OAuth services and validators
            services.RemoveAll<IValidateOptions<EmailLabeler.Configuration.GmailConfig>>();
            services.RemoveAll<GmailService>();
            services.RemoveAll<IGmailRepository>();
            services.RemoveAll<IEmailRepository>();

            // Register GmailService pointing at WireMock
            services.AddSingleton(_ => new GmailService(new BaseClientService.Initializer
            {
                BaseUri = WireMockBaseUrl.TrimEnd('/') + "/",
                ApplicationName = "EmailLabeler-Test"
            }));

            // Register GmailRepository using WireMock-backed GmailService
            services.AddSingleton<IGmailRepository>(sp => new GmailRepository(
                sp.GetRequiredService<GmailService>(),
                "me",
                "projects/test/topics/gmail",
                sp.GetRequiredService<ILogger<GmailRepository>>()));
            services.AddSingleton<IEmailRepository>(sp => sp.GetRequiredService<IGmailRepository>());
        });
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "EmailLabeler.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not find repo root (EmailLabeler.slnx)");
    }
}
