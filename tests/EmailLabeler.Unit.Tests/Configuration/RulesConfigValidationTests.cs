using EmailLabeler.Configuration;
using EmailLabeler.Domain;
using Microsoft.Extensions.Options;
using Xunit;

namespace EmailLabeler.Unit.Tests.Configuration;

public class RulesConfigValidationTests
{
    private readonly RulesConfigValidator _validator = new();

    [Fact]
    public void EmptyRules_ReturnsFailure()
    {
        var config = new RulesConfig { Rules = [] };

        var result = _validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.Contains("At least one rule must be defined", result.FailureMessage);
    }

    [Fact]
    public void RuleWithZeroActions_ReturnsFailure()
    {
        var config = new RulesConfig
        {
            Rules = [new Rule { Match = new MatchCondition { From = "@test.com" }, Actions = [] }]
        };

        var result = _validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.Contains("At least one action must be defined", result.FailureMessage);
    }

    [Fact]
    public void LabelActionWithEmptyLabel_ReturnsFailure()
    {
        var config = new RulesConfig
        {
            Rules =
            [
                new Rule
                {
                    Match = new MatchCondition { From = "@test.com" },
                    Actions = [new ActionConfig { Type = ActionType.Label, Label = "" }]
                }
            ]
        };

        var result = _validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.Contains("Label action must have a non-empty 'label' field", result.FailureMessage);
    }

    [Fact]
    public void ValidConfig_ReturnsSuccess()
    {
        var config = new RulesConfig
        {
            Rules =
            [
                new Rule
                {
                    Match = new MatchCondition { From = "@newsletter.com" },
                    Actions = [new ActionConfig { Type = ActionType.Label, Label = "Newsletters" }]
                }
            ]
        };

        var result = _validator.Validate(null, config);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ArchiveActionWithoutLabel_ReturnsSuccess()
    {
        var config = new RulesConfig
        {
            Rules =
            [
                new Rule
                {
                    Match = new MatchCondition { From = "@spam.com" },
                    Actions = [new ActionConfig { Type = ActionType.Archive }]
                }
            ]
        };

        var result = _validator.Validate(null, config);

        Assert.True(result.Succeeded);
    }
}
