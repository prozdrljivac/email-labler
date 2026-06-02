namespace EmailLabeler.Services;

using Sentry;

/// <summary>
/// Heartbeat notifier backed by Sentry Cron Monitoring. Each watch renewal sends a terminal
/// check-in (<see cref="CheckInStatus.Ok"/> or <see cref="CheckInStatus.Error"/>) and upserts the
/// monitor schedule so Sentry alerts on a missed cycle or a reported failure. When no Sentry DSN is
/// configured, the underlying <c>SentrySdk.CaptureCheckIn</c> call is a no-op, so local and CI
/// environments need no Sentry account.
/// </summary>
public class SentryCheckInNotifier : IHeartbeatNotifier
{
    /// <summary>Slug identifying the watch-renewal cron monitor in Sentry.</summary>
    public const string MonitorSlug = "gmail-watch-renewal";

    private readonly int _intervalDays;
    private readonly TimeSpan _checkInMargin;

    /// <summary>Initializes a new instance of <see cref="SentryCheckInNotifier"/>.</summary>
    public SentryCheckInNotifier(IConfiguration configuration)
    {
        var days = configuration.GetValue("WatchRenewal:IntervalDays", 6.0);
        _intervalDays = Math.Max(1, (int)Math.Round(days));
        var graceHours = configuration.GetValue("WatchRenewal:HealthGraceHours", 12.0);
        _checkInMargin = TimeSpan.FromHours(graceHours);
    }

    /// <inheritdoc/>
    public Task SignalSuccessAsync(CancellationToken cancellationToken)
    {
        Capture(CheckInStatus.Ok);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SignalFailureAsync(CancellationToken cancellationToken)
    {
        Capture(CheckInStatus.Error);
        return Task.CompletedTask;
    }

    private void Capture(CheckInStatus status) =>
        SentrySdk.CaptureCheckIn(MonitorSlug, status, configureMonitorOptions: options =>
        {
            options.Interval(_intervalDays, SentryMonitorInterval.Day);
            options.CheckInMargin = _checkInMargin;
        });
}
