namespace EmailLabeler.Engine;

using EmailLabeler.Configuration;
using EmailLabeler.Domain;
using EmailLabeler.Ports;
using Microsoft.Extensions.Options;

/// <summary>Orchestrates the full email processing flow.</summary>
public class EmailProcessor
{
    private readonly IEmailRepository _repo;
    private readonly IOptions<RulesConfig> _rulesConfig;
    private readonly IEnumerable<IEmailAction> _actions;
    private readonly ILogger<EmailProcessor> _logger;

    /// <summary>Initializes a new instance of <see cref="EmailProcessor"/>.</summary>
    public EmailProcessor(
        IEmailRepository repo,
        IOptions<RulesConfig> rulesConfig,
        IEnumerable<IEmailAction> actions,
        ILogger<EmailProcessor> logger)
    {
        _repo = repo;
        _rulesConfig = rulesConfig;
        _actions = actions;
        _logger = logger;
    }

    /// <summary>Processes a single email message by evaluating rules and executing matched actions.</summary>
    public async Task ProcessAsync(string messageId)
    {
        var email = await _repo.GetEmailAsync(messageId);
        var matches = RuleEngine.Evaluate(email, _rulesConfig.Value.Rules).ToList();

        if (matches.Count == 0)
        {
            _logger.LogInformation(
                "No rules matched for email {MessageId} from {From}",
                messageId, email.From);
            return;
        }

        foreach (var (rule, actionConfig) in matches)
        {
            var handler = _actions.FirstOrDefault(a => a.Type == actionConfig.Type);
            if (handler is null)
            {
                _logger.LogWarning("No handler for action type {ActionType}", actionConfig.Type);
                continue;
            }
            await handler.ExecuteAsync(email, actionConfig, _repo);
        }
    }
}
