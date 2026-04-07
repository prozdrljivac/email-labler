using EmailLabeler.Domain;
using EmailLabeler.Engine;
using Xunit;

namespace EmailLabeler.Unit.Tests.Engine;

public class RuleEngineTests
{
    private static Email MakeEmail(string from = "sender@newsletter.com") =>
        new("msg1", from, "Test Subject", ["INBOX"]);

    private static Rule MakeRule(string from, params ActionConfig[] actions) =>
        new() { Match = new MatchCondition { From = from }, Actions = actions };

    private static ActionConfig LabelAction(string label) =>
        new() { Type = ActionType.Label, Label = label };

    private static ActionConfig ArchiveAction() =>
        new() { Type = ActionType.Archive };

    [Fact]
    public void SingleMatchingRule_ReturnsItsActions()
    {
        var email = MakeEmail();
        var rule = MakeRule("@newsletter.com", LabelAction("Newsletters"));

        var results = RuleEngine.Evaluate(email, [rule]).ToList();

        Assert.Single(results);
        Assert.Equal("Newsletters", results[0].Action.Label);
    }

    [Fact]
    public void TwoMatchingRules_ReturnsAllActions()
    {
        var email = MakeEmail("alerts@newsletter.com");
        var rules = new[]
        {
            MakeRule("@newsletter.com", LabelAction("Newsletters")),
            MakeRule("alerts@", LabelAction("Alerts"), ArchiveAction())
        };

        var results = RuleEngine.Evaluate(email, rules).ToList();

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void NoRulesMatch_ReturnsEmpty()
    {
        var email = MakeEmail("user@other.com");
        var rule = MakeRule("@newsletter.com", LabelAction("Newsletters"));

        var results = RuleEngine.Evaluate(email, [rule]).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void CaseInsensitiveMatch()
    {
        var email = MakeEmail("BOSS@COMPANY.COM");
        var rule = MakeRule("boss@company.com", LabelAction("Boss"));

        var results = RuleEngine.Evaluate(email, [rule]).ToList();

        Assert.Single(results);
    }

    [Fact]
    public void NullFromCondition_DoesNotMatch()
    {
        var email = MakeEmail();
        var rule = new Rule
        {
            Match = new MatchCondition { From = null },
            Actions = [LabelAction("Test")]
        };

        var results = RuleEngine.Evaluate(email, [rule]).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void EmptyRulesCollection_ReturnsEmpty()
    {
        var email = MakeEmail();

        var results = RuleEngine.Evaluate(email, []).ToList();

        Assert.Empty(results);
    }
}
