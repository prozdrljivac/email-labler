namespace EmailLabeler.Configuration;

using Microsoft.Extensions.Options;

/// <summary>Validates GmailConfig on application startup.</summary>
public class GmailConfigValidator : IValidateOptions<GmailConfig>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, GmailConfig options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ClientId))
            errors.Add("GMAIL_CLIENT_ID environment variable is not set.");

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
            errors.Add("GMAIL_CLIENT_SECRET environment variable is not set.");

        if (string.IsNullOrWhiteSpace(options.RefreshToken))
            errors.Add("GMAIL_REFRESH_TOKEN environment variable is not set.");

        if (string.IsNullOrWhiteSpace(options.UserEmail))
            errors.Add("GMAIL_USER_EMAIL environment variable is not set.");

        if (string.IsNullOrWhiteSpace(options.TopicName))
            errors.Add("PUBSUB_TOPIC_NAME environment variable is not set.");

        if (string.IsNullOrWhiteSpace(options.ServiceAccountEmail))
            errors.Add("PUBSUB_SERVICE_ACCOUNT_EMAIL environment variable is not set.");

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
