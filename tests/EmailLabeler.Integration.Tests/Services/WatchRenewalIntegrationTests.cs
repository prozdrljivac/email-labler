using EmailLabeler.Integration.Tests.Fixtures;
using WireMock.Admin.Mappings;
using Xunit;

namespace EmailLabeler.Integration.Tests.Services;

[Collection(GmailCollection.Name)]
public class WatchRenewalIntegrationTests
{
    private readonly WireMockGmailFixture _fixture;

    public WatchRenewalIntegrationTests(WireMockGmailFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BackgroundService_RenewsWatch_ViGmailApi()
    {
        var ct = TestContext.Current.CancellationToken;
        await _fixture.AdminApi.ResetMappingsAsync(null, ct);
        await _fixture.AdminApi.DeleteRequestsAsync(ct);

        // Stub POST /gmail/v1/users/me/watch → 200 OK
        await _fixture.AdminApi.PostMappingAsync(new MappingModel
        {
            Request = new RequestModel { Methods = ["POST"], Path = "/gmail/v1/users/me/watch" },
            Response = new ResponseModel
            {
                StatusCode = 200,
                BodyAsJson = new { historyId = 123, expiration = 9999999999 }
            }
        }, ct);

        await using var factory = new CustomWebApplicationFactory
        {
            WireMockBaseUrl = _fixture.BaseUrl,
            ExtraConfig = new Dictionary<string, string?>
            {
                ["WatchRenewal:IntervalDays"] = "0.000001157" // ~100ms
            }
        };

        // Creating the server starts hosted services including WatchRenewalService
        _ = factory.Server;

        // Wait for the background service to fire at least once
        await Task.Delay(500, ct);

        var requests = await _fixture.AdminApi.GetRequestsAsync(ct);
        Assert.Contains(requests,
            r => r.Request!.Method == "POST" && r.Request!.Url!.Contains("/users/me/watch"));
    }
}
