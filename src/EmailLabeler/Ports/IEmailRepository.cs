namespace EmailLabeler.Ports;

using EmailLabeler.Domain;

/// <summary>Port for email provider operations. Provider-agnostic.</summary>
public interface IEmailRepository
{
    /// <summary>Retrieves an email by its message ID.</summary>
    Task<Email> GetEmailAsync(string messageId);

    /// <summary>Applies a label to the specified message.</summary>
    Task ApplyLabelAsync(string messageId, string labelName);

    /// <summary>Archives the specified message (removes from inbox).</summary>
    Task ArchiveAsync(string messageId);

    /// <summary>Ensures a label with the given name exists, creating it if necessary.</summary>
    Task EnsureLabelExistsAsync(string labelName);

    /// <summary>Renews the push notification watch subscription.</summary>
    Task RenewWatchAsync();

    /// <summary>Gets new message IDs from history since the given history ID.</summary>
    Task<IEnumerable<string>> GetNewMessageIdsAsync(ulong historyId);
}
