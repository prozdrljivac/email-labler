namespace EmailLabeler.Domain;

/// <summary>Configuration for a single action to execute on a matched email.</summary>
public class ActionConfig
{
    /// <summary>The type of action to execute.</summary>
    public ActionType Type { get; set; }

    /// <summary>The label name to apply (required for Label actions).</summary>
    public string? Label { get; set; }
}
