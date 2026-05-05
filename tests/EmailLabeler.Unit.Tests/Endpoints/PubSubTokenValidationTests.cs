using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EmailLabeler.Endpoints;
using EmailLabeler.Ports;
using EmailLabeler.Unit.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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

    private static WebApplicationFactory<Program> CreateFactory(IPubSubTokenValidator tokenValidator)
    {
        var mockRepo = Substitute.For<IEmailRepository>();
        mockRepo.GetNewMessageIdsAsync(Arg.Any<ulong>()).Returns(Enumerable.Empty<string>());

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(WebHostDefaults.ContentRootKey, TestHelper.RepoRoot);
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IValidateOptions<EmailLabeler.Configuration.GmailConfig>>();
                    services.RemoveAll<IEmailRepository>();
                    services.RemoveAll<EmailLabeler.Adapters.IGmailRepository>();
                    services.RemoveAll<IPubSubTokenValidator>();
                    services.AddSingleton(tokenValidator);
                    services.AddScoped<IEmailRepository>(_ => mockRepo);
                    services.Configure<EmailLabeler.Configuration.GmailConfig>(c =>
                        c.ServiceAccountEmail = "test@test.iam.gserviceaccount.com");
                });
            });
    }

    [Fact]
    public async Task ValidToken_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockValidator = Substitute.For<IPubSubTokenValidator>();
        mockValidator.ValidateAsync("valid-jwt", "test@test.iam.gserviceaccount.com")
            .Returns(true);

        await using var factory = CreateFactory(mockValidator);
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/labler");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-jwt");
        request.Content = JsonContent.Create(CreateValidPayload());

        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MissingAuthHeader_ReturnsUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockValidator = Substitute.For<IPubSubTokenValidator>();

        await using var factory = CreateFactory(mockValidator);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/labler", CreateValidPayload(), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidToken_ReturnsUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockValidator = Substitute.For<IPubSubTokenValidator>();
        mockValidator.ValidateAsync("invalid-jwt", "test@test.iam.gserviceaccount.com")
            .Returns(false);

        await using var factory = CreateFactory(mockValidator);
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/labler");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-jwt");
        request.Content = JsonContent.Create(CreateValidPayload());

        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
