namespace EmailLabeler.Services;

/// <summary>
/// Shared, thread-safe record of the most recent successful watch renewal.
/// Written by <see cref="WatchRenewalService"/> and read by the watch-renewal health check.
/// </summary>
public class WatchRenewalState
{
    private readonly Lock _gate = new();
    private DateTimeOffset? _lastSuccessfulRenewal;

    /// <summary>Timestamp of the last successful watch renewal, or <c>null</c> if none has succeeded yet.</summary>
    public DateTimeOffset? LastSuccessfulRenewal
    {
        get
        {
            lock (_gate)
                return _lastSuccessfulRenewal;
        }
    }

    /// <summary>Records a successful renewal at the given timestamp.</summary>
    public void MarkRenewed(DateTimeOffset timestamp)
    {
        lock (_gate)
            _lastSuccessfulRenewal = timestamp;
    }
}
