namespace EmailLabeler.Health;

using EmailLabeler.Adapters;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Verifies that the Gmail API is reachable and the configured credentials are valid,
/// rather than merely confirming the process is alive.
/// </summary>
public class GmailConnectivityHealthCheck : IHealthCheck
{
    private readonly IGmailRepository _repository;
    private readonly ILogger<GmailConnectivityHealthCheck> _logger;

    /// <summary>Initializes a new instance of <see cref="GmailConnectivityHealthCheck"/>.</summary>
    public GmailConnectivityHealthCheck(
        IGmailRepository repository,
        ILogger<GmailConnectivityHealthCheck> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _repository.CheckConnectivityAsync();
            return HealthCheckResult.Healthy("Gmail API reachable and credentials valid.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gmail connectivity health check failed");
            return HealthCheckResult.Unhealthy("Gmail API unreachable or credentials rejected.", ex);
        }
    }
}
