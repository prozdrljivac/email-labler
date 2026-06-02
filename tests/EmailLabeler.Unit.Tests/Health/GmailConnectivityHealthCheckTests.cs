using EmailLabeler.Adapters;
using EmailLabeler.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace EmailLabeler.Unit.Tests.Health;

public class GmailConnectivityHealthCheckTests
{
    [Fact]
    public async Task ConnectivitySucceeds_ReportsHealthy()
    {
        var repo = Substitute.For<IGmailRepository>();
        repo.CheckConnectivityAsync().Returns(Task.CompletedTask);
        var check = new GmailConnectivityHealthCheck(
            repo, NullLogger<GmailConnectivityHealthCheck>.Instance);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ConnectivityThrows_ReportsUnhealthy()
    {
        var repo = Substitute.For<IGmailRepository>();
        repo.CheckConnectivityAsync().Returns<Task>(_ => throw new InvalidOperationException("creds rejected"));
        var check = new GmailConnectivityHealthCheck(
            repo, NullLogger<GmailConnectivityHealthCheck>.Instance);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }
}
