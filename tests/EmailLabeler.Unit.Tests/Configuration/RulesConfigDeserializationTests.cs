using System.Text;
using EmailLabeler.Configuration;
using EmailLabeler.Domain;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EmailLabeler.Unit.Tests.Configuration;

public class RulesConfigDeserializationTests
{
    private static RulesConfig BindFromYaml(string yaml)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
        var config = new ConfigurationBuilder()
            .AddYamlStream(stream)
            .Build();

        var rulesConfig = new RulesConfig();
        config.Bind(rulesConfig);
        return rulesConfig;
    }

    [Fact]
    public void SingleRule_WithLabelAction_BindsCorrectly()
    {
        const string yaml = """
            rules:
              - match:
                  from: "@newsletter.com"
                actions:
                  - type: label
                    label: "Newsletters"
            """;

        var result = BindFromYaml(yaml);

        Assert.Single(result.Rules);
        Assert.Equal("@newsletter.com", result.Rules[0].Match.From);
        Assert.Single(result.Rules[0].Actions);
        Assert.Equal(ActionType.Label, result.Rules[0].Actions[0].Type);
        Assert.Equal("Newsletters", result.Rules[0].Actions[0].Label);
    }

    [Fact]
    public void MultipleRules_BindCorrectly()
    {
        const string yaml = """
            rules:
              - match:
                  from: "@newsletter.com"
                actions:
                  - type: label
                    label: "Newsletters"
              - match:
                  from: "@alerts.example.com"
                actions:
                  - type: label
                    label: "Alerts"
                  - type: archive
            """;

        var result = BindFromYaml(yaml);

        Assert.Equal(2, result.Rules.Count);

        Assert.Equal("@newsletter.com", result.Rules[0].Match.From);
        Assert.Single(result.Rules[0].Actions);

        Assert.Equal("@alerts.example.com", result.Rules[1].Match.From);
        Assert.Equal(2, result.Rules[1].Actions.Length);
        Assert.Equal(ActionType.Label, result.Rules[1].Actions[0].Type);
        Assert.Equal("Alerts", result.Rules[1].Actions[0].Label);
        Assert.Equal(ActionType.Archive, result.Rules[1].Actions[1].Type);
    }

    [Fact]
    public void ArchiveAction_HasNullLabel()
    {
        const string yaml = """
            rules:
              - match:
                  from: "@spam.com"
                actions:
                  - type: archive
            """;

        var result = BindFromYaml(yaml);

        Assert.Single(result.Rules);
        Assert.Equal(ActionType.Archive, result.Rules[0].Actions[0].Type);
        Assert.Null(result.Rules[0].Actions[0].Label);
    }
}
