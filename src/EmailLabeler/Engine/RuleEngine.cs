namespace EmailLabeler.Engine;

using EmailLabeler.Domain;

/// <summary>Evaluates rules against emails. Pure logic, no I/O.</summary>
public static class RuleEngine
{
    /// <summary>Returns all matching (rule, action) pairs for the given email.</summary>
    public static IEnumerable<(Rule Rule, ActionConfig Action)> Evaluate(
        Email email, IEnumerable<Rule> rules)
    {
        foreach (var rule in rules)
        {
            if (!Matches(email, rule.Match))
                continue;

            foreach (var action in rule.Actions)
                yield return (rule, action);
        }
    }

    private static bool Matches(Email email, MatchCondition condition)
    {
        if (condition.From is not null)
            return email.From.Contains(condition.From, StringComparison.OrdinalIgnoreCase);

        return false;
    }
}
