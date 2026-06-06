namespace EmailLabeler.Adapters;

using System.Net;
using EmailLabeler.Domain;
using EmailLabeler.Ports;
using Google;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Microsoft.Extensions.Logging;

/// <summary>Gmail API adapter implementing the email repository port.</summary>
public class GmailRepository : IGmailRepository
{
    private readonly GmailService _gmail;
    private readonly string _userId;
    private readonly string _topicName;
    private readonly ILogger<GmailRepository> _logger;
    private readonly Dictionary<string, string> _labelCache = new();

    // Cursor tracking the last Gmail history ID we have processed. Gmail's history.list
    // returns records *after* StartHistoryId, so we must list from the previously seen
    // history ID — not from the notification's own (which already includes the new mail).
    // Seeded from the watch() baseline and advanced as notifications are processed.
    private readonly Lock _historyGate = new();
    private ulong? _lastHistoryId;

    /// <summary>Initializes a new instance of <see cref="GmailRepository"/>.</summary>
    public GmailRepository(
        GmailService gmail,
        string userId,
        string topicName,
        ILogger<GmailRepository> logger)
    {
        _gmail = gmail;
        _userId = userId;
        _topicName = topicName;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Email?> GetEmailAsync(string messageId)
    {
        Message msg;
        try
        {
            msg = await ExecuteGmailAsync(
                () => _gmail.Users.Messages.Get(_userId, messageId).ExecuteAsync());
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(ex, "Message {MessageId} no longer exists; skipping", messageId);
            return null;
        }

        var from = msg.Payload?.Headers?
            .FirstOrDefault(h => h.Name.Equals("From", StringComparison.OrdinalIgnoreCase))?.Value ?? "";
        var subject = msg.Payload?.Headers?
            .FirstOrDefault(h => h.Name.Equals("Subject", StringComparison.OrdinalIgnoreCase))?.Value ?? "";
        var labelIds = msg.LabelIds?.ToArray() ?? [];

        return new Email(msg.Id, from, subject, labelIds);
    }

    /// <inheritdoc/>
    public async Task ApplyLabelAsync(string messageId, string labelName)
    {
        var labelId = await GetLabelIdAsync(labelName);
        var body = new ModifyMessageRequest { AddLabelIds = [labelId] };
        await ExecuteGmailAsync(
            () => _gmail.Users.Messages.Modify(body, _userId, messageId).ExecuteAsync());
    }

    /// <inheritdoc/>
    public async Task ArchiveAsync(string messageId)
    {
        var body = new ModifyMessageRequest { RemoveLabelIds = ["INBOX"] };
        await ExecuteGmailAsync(
            () => _gmail.Users.Messages.Modify(body, _userId, messageId).ExecuteAsync());
    }

    /// <inheritdoc/>
    public async Task EnsureLabelExistsAsync(string labelName)
    {
        if (_labelCache.ContainsKey(labelName))
            return;

        var response = await ExecuteGmailAsync(
            () => _gmail.Users.Labels.List(_userId).ExecuteAsync());
        var existing = response.Labels?.FirstOrDefault(
            l => l.Name.Equals(labelName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            _labelCache[labelName] = existing.Id;
            return;
        }

        var newLabel = await ExecuteGmailAsync(
            () => _gmail.Users.Labels.Create(new Label { Name = labelName }, _userId).ExecuteAsync());
        _labelCache[labelName] = newLabel.Id;
        _logger.LogInformation("Created label {LabelName} with ID {LabelId}", labelName, newLabel.Id);
    }

    /// <inheritdoc/>
    public async Task CheckConnectivityAsync()
    {
        await ExecuteGmailAsync(
            () => _gmail.Users.GetProfile(_userId).ExecuteAsync());
    }

    /// <inheritdoc/>
    public async Task RenewWatchAsync()
    {
        var request = new WatchRequest { TopicName = _topicName };
        var response = await ExecuteGmailAsync(
            () => _gmail.Users.Watch(request, _userId).ExecuteAsync());

        // Seed the history cursor from the watch baseline so the first notification after
        // (re)starting has a valid point to list changes from. Only seed if unset, to avoid
        // rewinding a cursor that live notifications have already advanced.
        if (response.HistoryId is { } baseline)
        {
            lock (_historyGate)
                _lastHistoryId ??= baseline;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetNewMessageIdsAsync(ulong historyId)
    {
        // List from the last history ID we processed, not the notification's own. The
        // notification ID already reflects the new mail, so listing from it returns nothing.
        // Fall back to the notification ID only if we have no cursor yet (cold start before
        // the first watch renewal) — the next notification then advances normally.
        ulong startHistoryId;
        lock (_historyGate)
            startHistoryId = _lastHistoryId ?? historyId;

        var messageIds = new List<string>();
        var request = _gmail.Users.History.List(_userId);
        request.StartHistoryId = startHistoryId;
        request.HistoryTypes = UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.MessageAdded;

        try
        {
            string? pageToken = null;
            do
            {
                request.PageToken = pageToken;
                var response = await ExecuteGmailAsync(() => request.ExecuteAsync());
                if (response.History is { } history)
                {
                    messageIds.AddRange(history
                        .SelectMany(h => h.MessagesAdded ?? [])
                        .Select(m => m.Message.Id));
                }

                pageToken = response.NextPageToken;
            }
            while (pageToken is not null);

            AdvanceHistoryCursor(historyId);
            return messageIds.Distinct();
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            // The start point is older than Gmail retains. Reset the cursor to the
            // notification ID so we resume cleanly from the next change.
            _logger.LogWarning(
                ex, "Gmail history ID {HistoryId} is stale; resetting cursor", startHistoryId);
            AdvanceHistoryCursor(historyId);
            return [];
        }
    }

    private void AdvanceHistoryCursor(ulong historyId)
    {
        lock (_historyGate)
        {
            if (_lastHistoryId is null || historyId > _lastHistoryId)
                _lastHistoryId = historyId;
        }
    }

    private async Task<string> GetLabelIdAsync(string labelName)
    {
        if (_labelCache.TryGetValue(labelName, out var cached))
            return cached;

        await EnsureLabelExistsAsync(labelName);
        return _labelCache[labelName];
    }

    private static async Task<T> ExecuteGmailAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (TokenResponseException ex)
        {
            throw new EmailAuthenticationException("Email provider credentials were rejected.", ex);
        }
    }
}
