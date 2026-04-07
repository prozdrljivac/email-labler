namespace EmailLabeler.Adapters;

using EmailLabeler.Domain;
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
    public async Task<Email> GetEmailAsync(string messageId)
    {
        var msg = await _gmail.Users.Messages.Get(_userId, messageId).ExecuteAsync();

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
        await _gmail.Users.Messages.Modify(body, _userId, messageId).ExecuteAsync();
    }

    /// <inheritdoc/>
    public async Task ArchiveAsync(string messageId)
    {
        var body = new ModifyMessageRequest { RemoveLabelIds = ["INBOX"] };
        await _gmail.Users.Messages.Modify(body, _userId, messageId).ExecuteAsync();
    }

    /// <inheritdoc/>
    public async Task EnsureLabelExistsAsync(string labelName)
    {
        if (_labelCache.ContainsKey(labelName))
            return;

        var response = await _gmail.Users.Labels.List(_userId).ExecuteAsync();
        var existing = response.Labels?.FirstOrDefault(
            l => l.Name.Equals(labelName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            _labelCache[labelName] = existing.Id;
            return;
        }

        var newLabel = await _gmail.Users.Labels.Create(
            new Label { Name = labelName }, _userId).ExecuteAsync();
        _labelCache[labelName] = newLabel.Id;
        _logger.LogInformation("Created label {LabelName} with ID {LabelId}", labelName, newLabel.Id);
    }

    /// <inheritdoc/>
    public async Task RenewWatchAsync()
    {
        var request = new WatchRequest { TopicName = _topicName };
        await _gmail.Users.Watch(request, _userId).ExecuteAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetNewMessageIdsAsync(ulong historyId)
    {
        var request = _gmail.Users.History.List(_userId);
        request.StartHistoryId = historyId;
        request.HistoryTypes = UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.MessageAdded;

        var response = await request.ExecuteAsync();
        return response.History?
            .SelectMany(h => h.MessagesAdded ?? [])
            .Select(m => m.Message.Id)
            .Distinct()
            ?? [];
    }

    private async Task<string> GetLabelIdAsync(string labelName)
    {
        if (_labelCache.TryGetValue(labelName, out var cached))
            return cached;

        await EnsureLabelExistsAsync(labelName);
        return _labelCache[labelName];
    }
}
