namespace EmailLabeler.Actions;

using EmailLabeler.Domain;
using EmailLabeler.Ports;

/// <summary>Archives an email by removing it from the inbox.</summary>
public class ArchiveAction : IEmailAction
{
    /// <inheritdoc/>
    public ActionType Type => ActionType.Archive;

    /// <inheritdoc/>
    public async Task ExecuteAsync(Email email, ActionConfig config, IEmailRepository repo) => await repo.ArchiveAsync(email.Id);
}
