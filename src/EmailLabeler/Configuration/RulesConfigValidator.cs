namespace EmailLabeler.Configuration;

using EmailLabeler.Domain;
using Microsoft.Extensions.Options;

/// <summary>Validates RulesConfig on application startup.</summary>
public class RulesConfigValidator : IValidateOptions<RulesConfig>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, RulesConfig options)
    {
        var errors = new List<string>();

        if (options.Rules is not { Count: > 0 })
        {
            errors.Add("At least one rule must be defined in config.yaml.");
            return ValidateOptionsResult.Fail(errors); // no point continuing
        }

        errors.AddRange(options.Rules.SelectMany(ValidateRule));

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static IEnumerable<string> ValidateRule(Rule rule, int i)
    {
        if (rule.Actions is not { Length: > 0 })
        {
            yield return $"Rule[{i}]: At least one action must be defined.";
            yield break;
        }

        foreach (var action in rule.Actions.Where(a => a.Type == ActionType.Label && string.IsNullOrWhiteSpace(a.Label)))
            yield return $"Rule[{i}]: Label action must have a non-empty 'label' field.";
    }
}