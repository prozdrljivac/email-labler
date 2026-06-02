namespace EmailLabeler.Health;

using EmailLabeler.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Reports unhealthy when the Gmail watch subscription has not been renewed recently enough,
/// which would otherwise let the watch silently expire (7-day lifetime) and stop push delivery.
/// </summary>
public class WatchRenewalHealthCheck : IHealthCheck
{
    private readonly WatchRenewalState _state;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _staleThreshold;

    /// <summary>Initializes a new instance of <see cref="WatchRenewalHealthCheck"/>.</summary>
    public WatchRenewalHealthCheck(
        WatchRenewalState state,
        TimeProvider timeProvider,
        IConfiguration configuration)
    {
        _state = state;
        _timeProvider = timeProvider;

        // A renewal cycle runs every IntervalDays; allow a grace period on top before
        // calling it stale, so we don't flap right before each scheduled renewal.
        var intervalDays = configuration.GetValue("WatchRenewal:IntervalDays", 6.0);
        var graceHours = configuration.GetValue("WatchRenewal:HealthGraceHours", 12.0);
        _staleThreshold = TimeSpan.FromDays(intervalDays) + TimeSpan.FromHours(graceHours);
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var last = _state.LastSuccessfulRenewal;

        if (last is null)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "No watch renewal has completed yet since startup."));
        }

        var age = _timeProvider.GetUtcNow() - last.Value;
        var data = new Dictionary<string, object>
        {
            ["lastSuccessfulRenewal"] = last.Value,
            ["ageSeconds"] = (long)age.TotalSeconds,
            ["staleThresholdSeconds"] = (long)_staleThreshold.TotalSeconds,
        };

        if (age > _staleThreshold)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Watch last renewed {age.TotalHours:F1}h ago, exceeding the {_staleThreshold.TotalHours:F1}h threshold.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Watch renewed {age.TotalHours:F1}h ago.",
            data: data));
    }
}
