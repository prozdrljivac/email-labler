namespace EmailLabeler.Domain;

/// <summary>Conditions that determine whether a rule matches an email.</summary>
public class MatchCondition
{
    /// <summary>Substring to match against the email's From header (case-insensitive).</summary>
    public string? From { get; set; }
}
