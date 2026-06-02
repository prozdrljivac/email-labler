namespace EmailLabeler.Services;

/// <summary>
/// Sends outbound heartbeat signals to an external dead-man's-switch monitor (e.g. Sentry Cron
/// Monitoring) so that a silently stalled background job can be detected by the absence of signals.
/// </summary>
public interface IHeartbeatNotifier
{
    /// <summary>Signals that the monitored job completed successfully.</summary>
    Task SignalSuccessAsync(CancellationToken cancellationToken);

    /// <summary>Signals that the monitored job failed this cycle.</summary>
    Task SignalFailureAsync(CancellationToken cancellationToken);
}
