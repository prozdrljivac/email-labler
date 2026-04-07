using EmailLabeler.Ports;
using EmailLabeler.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace EmailLabeler.Unit.Tests.Services;

public class WatchRenewalServiceTests
{
    private static IServiceScopeFactory CreateScopeFactory(IEmailRepository repo)
    {
        var sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(IEmailRepository)).Returns(repo);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(sp);

        var factory = Substitute.For<IServiceScopeFactory>();
        factory.CreateScope().Returns(scope);

        return factory;
    }

    [Fact]
    public async Task RenewWatchAsync_CalledMultipleTimes()
    {
        var repo = Substitute.For<IEmailRepository>();
        repo.RenewWatchAsync().Returns(Task.CompletedTask);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WatchRenewal:IntervalDays"] = "0.000001157" // ~100ms
            })
            .Build();

        var service = new WatchRenewalService(
            CreateScopeFactory(repo), NullLogger<WatchRenewalService>.Instance, config);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(350));
        await service.StartAsync(cts.Token);

        try { await Task.Delay(400, cts.Token); } catch (OperationCanceledException) { }
        await service.StopAsync(CancellationToken.None);

        var renewCalls = repo.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == nameof(IEmailRepository.RenewWatchAsync));
        Assert.True(renewCalls >= 2, $"Expected at least 2 calls but got {renewCalls}");
    }

    [Fact]
    public async Task SurvivesFailure_ContinuesRenewing()
    {
        var repo = Substitute.For<IEmailRepository>();
        var callCount = 0;
        repo.RenewWatchAsync().Returns(_ =>
        {
            if (Interlocked.Increment(ref callCount) == 1)
                throw new InvalidOperationException("Transient failure");
            return Task.CompletedTask;
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WatchRenewal:IntervalDays"] = "0.000001157" // ~100ms
            })
            .Build();

        var service = new WatchRenewalService(
            CreateScopeFactory(repo), NullLogger<WatchRenewalService>.Instance, config);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(350));
        await service.StartAsync(cts.Token);

        try { await Task.Delay(400, cts.Token); } catch (OperationCanceledException) { }
        await service.StopAsync(CancellationToken.None);

        var renewCalls = repo.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == nameof(IEmailRepository.RenewWatchAsync));
        Assert.True(renewCalls >= 2, $"Expected at least 2 calls but got {renewCalls}");
    }
}
