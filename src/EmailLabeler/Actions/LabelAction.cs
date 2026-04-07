namespace EmailLabeler.Actions;

using EmailLabeler.Domain;
using EmailLabeler.Ports;

/// <summary>Ensures the label exists and applies it to the email.</summary>
public class LabelAction : IEmailAction
{
    /// <inheritdoc/>
    public ActionType Type => ActionType.Label;

    /// <inheritdoc/>
    public async Task ExecuteAsync(Email email, ActionConfig config, IEmailRepository repo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Label, nameof(config.Label));
        await repo.EnsureLabelExistsAsync(config.Label);
        await repo.ApplyLabelAsync(email.Id, config.Label);
    }
}
