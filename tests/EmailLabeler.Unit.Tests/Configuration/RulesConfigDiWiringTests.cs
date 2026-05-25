using System.Text;
using EmailLabeler.Configuration;
using EmailLabeler.Domain;
using EmailLabeler.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace EmailLabeler.Unit.Tests.Configuration;

public class RulesConfigDiWiringTests
{
    [Fact]
    public void AddRulesConfig_ResolvesOptionsWithExpectedRules()
    {
        const string yaml = """
            rules:
              - match:
                  from: "@newsletter.com"
                actions:
                  - type: label
                    label: "Newsletters"
            """;

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
        var config = new ConfigurationBuilder()
            .AddYamlStream(stream)
            .Build();

        var services = new ServiceCollection();
        services.AddRulesConfig(config);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RulesConfig>>();

        Assert.Single(options.Value.Rules);
        Assert.Equal("@newsletter.com", options.Value.Rules[0].Match.From);
        Assert.Equal(ActionType.Label, options.Value.Rules[0].Actions[0].Type);
        Assert.Equal("Newsletters", options.Value.Rules[0].Actions[0].Label);
    }

    [Fact]
    public void AddGmailIntegration_BindsEnvironmentVariablesAndRegistersRepository()
    {
        var previousValues = GmailEnvironmentNames.ToDictionary(
            name => name,
            Environment.GetEnvironmentVariable);

        try
        {
            SetGmailEnvironment();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGmailIntegration();

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<GmailConfig>>().Value;

            Assert.Equal("client-id", options.ClientId);
            Assert.Equal("client-secret", options.ClientSecret);
            Assert.Equal("refresh-token", options.RefreshToken);
            Assert.Equal("user@example.com", options.UserEmail);
            Assert.Equal("projects/test/topics/gmail", options.TopicName);
            Assert.Equal("push@test.iam.gserviceaccount.com", options.ServiceAccountEmail);
            Assert.Same(
                provider.GetRequiredService<EmailLabeler.Adapters.IGmailRepository>(),
                provider.GetRequiredService<IEmailRepository>());
        }
        finally
        {
            foreach (var (name, value) in previousValues)
                Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static readonly string[] GmailEnvironmentNames =
    [
        "GMAIL_CLIENT_ID",
        "GMAIL_CLIENT_SECRET",
        "GMAIL_REFRESH_TOKEN",
        "GMAIL_USER_EMAIL",
        "PUBSUB_TOPIC_NAME",
        "PUBSUB_SERVICE_ACCOUNT_EMAIL"
    ];

    private static void SetGmailEnvironment()
    {
        Environment.SetEnvironmentVariable("GMAIL_CLIENT_ID", "client-id");
        Environment.SetEnvironmentVariable("GMAIL_CLIENT_SECRET", "client-secret");
        Environment.SetEnvironmentVariable("GMAIL_REFRESH_TOKEN", "refresh-token");
        Environment.SetEnvironmentVariable("GMAIL_USER_EMAIL", "user@example.com");
        Environment.SetEnvironmentVariable("PUBSUB_TOPIC_NAME", "projects/test/topics/gmail");
        Environment.SetEnvironmentVariable("PUBSUB_SERVICE_ACCOUNT_EMAIL", "push@test.iam.gserviceaccount.com");
    }
}
