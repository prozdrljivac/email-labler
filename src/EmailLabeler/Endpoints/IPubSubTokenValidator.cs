namespace EmailLabeler.Endpoints;

/// <summary>Validates Google Pub/Sub OIDC JWT tokens.</summary>
public interface IPubSubTokenValidator
{
    /// <summary>Validates a JWT token and checks the email claim matches the expected service account.</summary>
    Task<bool> ValidateAsync(string token, string expectedEmail);
}
