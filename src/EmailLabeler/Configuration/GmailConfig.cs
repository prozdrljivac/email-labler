namespace EmailLabeler.Configuration;

/// <summary>Configuration for Gmail API integration, bound from environment variables.</summary>
public class GmailConfig
{
    /// <summary>OAuth2 client ID.</summary>
    public string ClientId { get; set; } = "";

    /// <summary>OAuth2 client secret.</summary>
    public string ClientSecret { get; set; } = "";

    /// <summary>OAuth2 refresh token.</summary>
    public string RefreshToken { get; set; } = "";

    /// <summary>Gmail user email address.</summary>
    public string UserEmail { get; set; } = "";

    /// <summary>Pub/Sub topic name for push notifications.</summary>
    public string TopicName { get; set; } = "";
}
