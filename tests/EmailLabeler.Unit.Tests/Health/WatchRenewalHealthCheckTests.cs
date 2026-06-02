using EmailLabeler.Health;
using EmailLabeler.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace EmailLabeler.Unit.Tests.Health;

public class WatchRenewalHealthCheckTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static IConfiguration Config() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WatchRenewal:IntervalDays"] = "6",
                ["WatchRenewal:HealthGraceHours"] = "12",
            })
            .Build();

    private static WatchRenewalHealthCheck Create(WatchRenewalState state, DateTimeOffset now) =>
        new(state, new FixedTimeProvider(now), Config());

    [Fact]
    public async Task NoRenewalYet_ReportsDegraded()
    {
        var now = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);
        var check = Create(new WatchRenewalState(), now);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task RecentRenewal_ReportsHealthy()
    {
        var now = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);
        var state = new WatchRenewalState();
        state.MarkRenewed(now - TimeSpan.FromDays(1)); // well within 6d + 12h
        var check = Create(state, now);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task StaleRenewal_ReportsUnhealthy()
    {
        var now = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);
        var state = new WatchRenewalState();
        state.MarkRenewed(now - TimeSpan.FromDays(7)); // beyond 6d + 12h threshold
        var check = Create(state, now);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
