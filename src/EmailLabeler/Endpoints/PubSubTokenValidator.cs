namespace EmailLabeler.Endpoints;

using Google.Apis.Auth;

/// <summary>Validates Google Pub/Sub OIDC JWT tokens using Google's public keys.</summary>
public class PubSubTokenValidator : IPubSubTokenValidator
{
    /// <inheritdoc/>
    public async Task<bool> ValidateAsync(string token, string expectedEmail)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(token);
            return string.Equals(payload.Email, expectedEmail, StringComparison.OrdinalIgnoreCase);
        }
        catch (InvalidJwtException)
        {
            return false;
        }
    }
}
