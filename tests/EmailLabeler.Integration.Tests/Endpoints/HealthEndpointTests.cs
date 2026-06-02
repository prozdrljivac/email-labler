using System.Net;
using EmailLabeler.Integration.Tests.Fixtures;
using WireMock.Admin.Mappings;
using Xunit;

namespace EmailLabeler.Integration.Tests.Endpoints;

[Collection(GmailCollection.Name)]
public class HealthEndpointTests
{
    private readonly WireMockGmailFixture _fixture;

    public HealthEndpointTests(WireMockGmailFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Health_WhenGmailReachable_ReturnsOkWithCheckDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        await _fixture.AdminApi.ResetMappingsAsync(null, ct);
        await _fixture.AdminApi.DeleteRequestsAsync(ct);

        // Stub the lightweight connectivity probe (users.getProfile).
        await _fixture.AdminApi.PostMappingAsync(new MappingModel
        {
            Request = new RequestModel { Methods = ["GET"], Path = "/gmail/v1/users/me/profile" },
            Response = new ResponseModel
            {
                StatusCode = 200,
                BodyAsJson = new { emailAddress = "user@gmail.com", messagesTotal = 1 }
            }
        }, ct);

        await using var factory = new CustomWebApplicationFactory { WireMockBaseUrl = _fixture.BaseUrl };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", ct);

        // Gmail healthy; watch-renewal degraded (no renewal yet) → overall non-failing → 200.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("gmail", body);
        Assert.Contains("watch-renewal", body);
    }

    [Fact]
    public async Task Health_WhenGmailUnreachable_ReturnsServiceUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        await _fixture.AdminApi.ResetMappingsAsync(null, ct);
        await _fixture.AdminApi.DeleteRequestsAsync(ct);

        // No profile stub → WireMock 404 → Gmail connectivity check fails → overall unhealthy.
        await using var factory = new CustomWebApplicationFactory { WireMockBaseUrl = _fixture.BaseUrl };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
