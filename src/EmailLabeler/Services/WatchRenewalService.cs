namespace EmailLabeler.Services;

using EmailLabeler.Ports;

/// <summary>Background service that periodically renews the Gmail watch subscription.</summary>
public class WatchRenewalService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WatchRenewalService> _logger;
    private readonly WatchRenewalState _state;
    private readonly TimeProvider _timeProvider;
    private readonly IHeartbeatNotifier _heartbeat;
    private readonly TimeSpan _interval;

    /// <summary>Initializes a new instance of <see cref="WatchRenewalService"/>.</summary>
    public WatchRenewalService(
        IServiceScopeFactory scopeFactory,
        ILogger<WatchRenewalService> logger,
        IConfiguration configuration,
        WatchRenewalState state,
        TimeProvider timeProvider,
        IHeartbeatNotifier heartbeat)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _state = state;
        _timeProvider = timeProvider;
        _heartbeat = heartbeat;
        var days = configuration.GetValue("WatchRenewal:IntervalDays", 6.0);
        _interval = TimeSpan.FromDays(days);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IEmailRepository>();
                await repo.RenewWatchAsync();
                _state.MarkRenewed(_timeProvider.GetUtcNow());
                _logger.LogInformation("Watch subscription renewed successfully");
                await _heartbeat.SignalSuccessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to renew watch subscription");
                await _heartbeat.SignalFailureAsync(stoppingToken);
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
