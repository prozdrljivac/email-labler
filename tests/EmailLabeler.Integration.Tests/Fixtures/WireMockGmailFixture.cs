using WireMock.Client;
using WireMock.Net.Testcontainers;
using Xunit;

namespace EmailLabeler.Integration.Tests.Fixtures;

public class WireMockGmailFixture : IAsyncLifetime
{
    public WireMockContainer Container { get; private set; } = null!;
    public IWireMockAdminApi AdminApi { get; private set; } = null!;
    public string BaseUrl => Container.GetPublicUrl();

    public async ValueTask InitializeAsync()
    {
        Container = new WireMockContainerBuilder()
            .Build();
        await Container.StartAsync();
        AdminApi = Container.CreateWireMockAdminClient();
    }

    public async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}
