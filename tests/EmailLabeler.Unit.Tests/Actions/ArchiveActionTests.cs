using EmailLabeler.Actions;
using EmailLabeler.Domain;
using EmailLabeler.Ports;
using NSubstitute;
using Xunit;

namespace EmailLabeler.Unit.Tests.Actions;

public class ArchiveActionTests
{
    [Fact]
    public async Task ExecuteAsync_CallsArchiveOnce()
    {
        var repo = Substitute.For<IEmailRepository>();
        var sut = new ArchiveAction();
        var email = new Email("msg1", "sender@test.com", "Subject", ["INBOX"]);
        var config = new ActionConfig { Type = ActionType.Archive };

        await sut.ExecuteAsync(email, config, repo);

        await repo.Received(1).ArchiveAsync("msg1");
    }
}
