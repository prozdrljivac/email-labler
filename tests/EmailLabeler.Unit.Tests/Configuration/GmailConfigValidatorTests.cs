using EmailLabeler.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace EmailLabeler.Unit.Tests.Configuration;

public class GmailConfigValidatorTests
{
    private readonly GmailConfigValidator _validator = new();

    private static GmailConfig ValidConfig() => new()
    {
        ClientId = "test-client-id",
        ClientSecret = "test-client-secret",
        RefreshToken = "test-refresh-token",
        UserEmail = "user@example.com",
        TopicName = "projects/my-project/topics/gmail"
    };

    [Fact]
    public void AllFieldsPopulated_ReturnsSuccess()
    {
        var result = _validator.Validate(null, ValidConfig());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void MissingClientId_ReturnsFailure()
    {
        var config = ValidConfig();
        config.ClientId = "";

        var result = _validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.Contains("GMAIL_CLIENT_ID", result.FailureMessage);
    }

    [Fact]
    public void MissingClientSecret_ReturnsFailure()
    {
        var config = ValidConfig();
        config.ClientSecret = "";

        var result = _validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.Contains("GMAIL_CLIENT_SECRET", result.FailureMessage);
    }

    [Fact]
    public void MissingRefreshToken_ReturnsFailure()
    {
        var config = ValidConfig();
        config.RefreshToken = "";

        var result = _validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.Contains("GMAIL_REFRESH_TOKEN", result.FailureMessage);
    }

    [Fact]
    public void MissingUserEmail_ReturnsFailure()
    {
        var config = ValidConfig();
        config.UserEmail = "";

        var result = _validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.Contains("GMAIL_USER_EMAIL", result.FailureMessage);
    }

    [Fact]
    public void MissingTopicName_ReturnsFailure()
    {
        var config = ValidConfig();
        config.TopicName = "";

        var result = _validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.Contains("PUBSUB_TOPIC_NAME", result.FailureMessage);
    }

    [Fact]
    public void AllFieldsMissing_ReturnsAllErrors()
    {
        var config = new GmailConfig();

        var result = _validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.Contains("GMAIL_CLIENT_ID", result.FailureMessage);
        Assert.Contains("GMAIL_CLIENT_SECRET", result.FailureMessage);
        Assert.Contains("GMAIL_REFRESH_TOKEN", result.FailureMessage);
        Assert.Contains("GMAIL_USER_EMAIL", result.FailureMessage);
        Assert.Contains("PUBSUB_TOPIC_NAME", result.FailureMessage);
    }
}
