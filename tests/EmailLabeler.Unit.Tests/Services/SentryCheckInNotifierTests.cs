using EmailLabeler.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EmailLabeler.Unit.Tests.Services;

public class SentryCheckInNotifierTests
{
    private static SentryCheckInNotifier Create() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WatchRenewal:IntervalDays"] = "6",
                ["WatchRenewal:HealthGraceHours"] = "12",
            })
            .Build());

    // With no Sentry DSN configured the SDK is disabled, so check-ins must be safe no-ops
    // rather than throwing — the renewal loop depends on this.
    [Fact]
    public async Task SignalSuccess_WhenSentryDisabled_DoesNotThrow()
    {
        var notifier = Create();
        await notifier.SignalSuccessAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SignalFailure_WhenSentryDisabled_DoesNotThrow()
    {
        var notifier = Create();
        await notifier.SignalFailureAsync(TestContext.Current.CancellationToken);
    }
}
