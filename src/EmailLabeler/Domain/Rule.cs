namespace EmailLabeler.Domain;

/// <summary>A single rule mapping match conditions to actions.</summary>
public class Rule
{
    /// <summary>The conditions that determine whether this rule matches an email.</summary>
    public MatchCondition Match { get; set; } = new();

    /// <summary>The actions to execute when this rule matches.</summary>
    public ActionConfig[] Actions { get; set; } = [];
}
