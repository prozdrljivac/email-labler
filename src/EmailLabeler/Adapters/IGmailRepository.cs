namespace EmailLabeler.Adapters;

using EmailLabeler.Ports;

/// <summary>Gmail-specific adapter contract.</summary>
public interface IGmailRepository : IEmailRepository
{
    /// <summary>
    /// Performs a lightweight call against the Gmail API to verify that credentials are
    /// valid and the service is reachable. Throws if connectivity or authentication fails.
    /// </summary>
    Task CheckConnectivityAsync();
}
