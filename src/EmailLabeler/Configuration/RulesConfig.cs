namespace EmailLabeler.Configuration;

using EmailLabeler.Domain;

/// <summary>Root configuration object deserialized from config.yaml.</summary>
public class RulesConfig
{
    /// <summary>The list of rules to evaluate against incoming emails.</summary>
    public List<Rule> Rules { get; set; } = new();
}
