using EmailLabeler.Actions;
using EmailLabeler.Domain;
using EmailLabeler.Ports;
using NSubstitute;
using Xunit;

namespace EmailLabeler.Unit.Tests.Actions;

public class LabelActionTests
{
    private readonly IEmailRepository _repo = Substitute.For<IEmailRepository>();
    private readonly LabelAction _sut = new();

    [Fact]
    public async Task ExecuteAsync_EnsuresLabelExistsThenAppliesIt()
    {
        var email = new Email("msg1", "sender@test.com", "Subject", ["INBOX"]);
        var config = new ActionConfig { Type = ActionType.Label, Label = "Newsletters" };

        await _sut.ExecuteAsync(email, config, _repo);

        Received.InOrder(() =>
        {
            _repo.EnsureLabelExistsAsync("Newsletters");
            _repo.ApplyLabelAsync("msg1", "Newsletters");
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithNullLabel_ThrowsArgumentException()
    {
        var email = new Email("msg1", "sender@test.com", "Subject", ["INBOX"]);
        var config = new ActionConfig { Type = ActionType.Label, Label = null };

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _sut.ExecuteAsync(email, config, _repo));
    }
}
