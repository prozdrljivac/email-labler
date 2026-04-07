using EmailLabeler.Configuration;
using EmailLabeler.Domain;
using EmailLabeler.Engine;
using EmailLabeler.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace EmailLabeler.Unit.Tests.Engine;

public class EmailProcessorTests
{
    private readonly IEmailRepository _repo = Substitute.For<IEmailRepository>();
    private readonly ILogger<EmailProcessor> _logger = Substitute.For<ILogger<EmailProcessor>>();

    private EmailProcessor CreateProcessor(RulesConfig config, params IEmailAction[] actions)
    {
        var options = Substitute.For<IOptions<RulesConfig>>();
        options.Value.Returns(config);
        return new EmailProcessor(_repo, options, actions, _logger);
    }

    [Fact]
    public async Task MatchesTwoRules_BothActionHandlersCalled()
    {
        var email = new Email("msg1", "alerts@newsletter.com", "Subject", ["INBOX"]);
        _repo.GetEmailAsync("msg1").Returns(email);

        var labelAction = Substitute.For<IEmailAction>();
        labelAction.Type.Returns(ActionType.Label);
        var archiveAction = Substitute.For<IEmailAction>();
        archiveAction.Type.Returns(ActionType.Archive);

        var config = new RulesConfig
        {
            Rules =
            [
                new Rule
                {
                    Match = new MatchCondition { From = "@newsletter.com" },
                    Actions = [new ActionConfig { Type = ActionType.Label, Label = "Newsletters" }]
                },
                new Rule
                {
                    Match = new MatchCondition { From = "alerts@" },
                    Actions = [new ActionConfig { Type = ActionType.Archive }]
                }
            ]
        };

        var processor = CreateProcessor(config, labelAction, archiveAction);
        await processor.ProcessAsync("msg1");

        await labelAction.Received(1).ExecuteAsync(
            email, Arg.Is<ActionConfig>(a => a.Label == "Newsletters"), _repo);
        await archiveAction.Received(1).ExecuteAsync(
            email, Arg.Is<ActionConfig>(a => a.Type == ActionType.Archive), _repo);
    }

    [Fact]
    public async Task NoRulesMatch_LogsInformation_NoRepoMutations()
    {
        var email = new Email("msg1", "user@other.com", "Subject", ["INBOX"]);
        _repo.GetEmailAsync("msg1").Returns(email);

        var config = new RulesConfig
        {
            Rules =
            [
                new Rule
                {
                    Match = new MatchCondition { From = "@newsletter.com" },
                    Actions = [new ActionConfig { Type = ActionType.Label, Label = "Newsletters" }]
                }
            ]
        };

        var processor = CreateProcessor(config);
        await processor.ProcessAsync("msg1");

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());

        await _repo.DidNotReceive().ApplyLabelAsync(Arg.Any<string>(), Arg.Any<string>());
        await _repo.DidNotReceive().ArchiveAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task UnknownActionType_LogsWarning_ContinuesProcessing()
    {
        var email = new Email("msg1", "sender@newsletter.com", "Subject", ["INBOX"]);
        _repo.GetEmailAsync("msg1").Returns(email);

        // Register only archive handler, but rule has label action
        var archiveAction = Substitute.For<IEmailAction>();
        archiveAction.Type.Returns(ActionType.Archive);

        var config = new RulesConfig
        {
            Rules =
            [
                new Rule
                {
                    Match = new MatchCondition { From = "@newsletter.com" },
                    Actions =
                    [
                        new ActionConfig { Type = ActionType.Label, Label = "Newsletters" },
                        new ActionConfig { Type = ActionType.Archive }
                    ]
                }
            ]
        };

        var processor = CreateProcessor(config, archiveAction);
        await processor.ProcessAsync("msg1");

        // Warning logged for missing label handler
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());

        // Archive action still executed
        await archiveAction.Received(1).ExecuteAsync(
            email, Arg.Is<ActionConfig>(a => a.Type == ActionType.Archive), _repo);
    }
}
