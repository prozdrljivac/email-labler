using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EmailLabeler.Ports;
using EmailLabeler.Unit.Tests.Helpers;
using EmailLabeler.Endpoints;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace EmailLabeler.Unit.Tests.Endpoints;

public class PubSubTokenValidationTests
{
    private static PubSubPushEnvelope CreateValidPayload()
    {
        var notification = new { emailAddress = "user@gmail.com", historyId = 12345 };
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(notification)));
        return new PubSubPushEnvelope(
            new PubSubMessagePayload(data, "mid-1", "2024-01-01T00:00:00Z"),
            "projects/test/subscriptions/gmail");
    }

    private static WebApplicationFactory<Program> CreateFactory(string token)
    {
        var mockRepo = Substitute.For<IEmailRepository>();
        mockRepo.GetNewMessageIdsAsync(Arg.Any<ulong>()).Returns(Enumerable.Empty<string>());

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(WebHostDefaults.ContentRootKey, TestHelper.RepoRoot);
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["PUBSUB_VERIFICATION_TOKEN"] = token,
                    });
                });
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IValidateOptions<EmailLabeler.Configuration.GmailConfig>>();
                    services.RemoveAll<IEmailRepository>();
                    services.RemoveAll<EmailLabeler.Adapters.IGmailRepository>();
                    services.AddScoped<IEmailRepository>(_ => mockRepo);
                });
            });
    }

    [Fact]
    public async Task ValidToken_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory("test-secret");
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/labler");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-secret");
        request.Content = JsonContent.Create(CreateValidPayload());

        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MissingAuthHeader_ReturnsUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory("test-secret");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/labler", CreateValidPayload(), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongToken_ReturnsUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory("test-secret");
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/labler");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-token");
        request.Content = JsonContent.Create(CreateValidPayload());

        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
