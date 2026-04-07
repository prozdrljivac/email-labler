using System.Text;
using EmailLabeler.Configuration;
using EmailLabeler.Domain;
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
}
