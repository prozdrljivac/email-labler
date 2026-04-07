using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EmailLabeler.Domain;
using EmailLabeler.Ports;
using EmailLabeler.Unit.Tests.Helpers;
using EmailLabeler.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace EmailLabeler.Unit.Tests.Endpoints;

public class LablerEndpointTests
{
    [Fact]
    public async Task ValidPubSubPayload_ProcessesMessageAndReturnsOk()
    {
        var mockRepo = Substitute.For<IEmailRepository>();
        mockRepo.GetNewMessageIdsAsync(12345).Returns(new[] { "msg123" });
        mockRepo.GetEmailAsync("msg123").Returns(
            new Email("msg123", "sender@example.com", "Test subject", ["INBOX"]));
        mockRepo.EnsureLabelExistsAsync(Arg.Any<string>()).Returns(Task.CompletedTask);
        mockRepo.ApplyLabelAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.ContentRootKey, TestHelper.RepoRoot);
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IValidateOptions<EmailLabeler.Configuration.GmailConfig>>();
                    services.RemoveAll<IEmailRepository>();
                    services.RemoveAll<EmailLabeler.Adapters.IGmailRepository>();
                    services.AddScoped<IEmailRepository>(_ => mockRepo);
                });
            });

        using var client = factory.CreateClient();

        var notification = new { emailAddress = "user@gmail.com", historyId = 12345 };
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(notification)));
        var payload = new PubSubPushEnvelope(
            new PubSubMessagePayload(data, "mid-1", "2024-01-01T00:00:00Z"),
            "projects/test/subscriptions/gmail");

        var ct = TestContext.Current.CancellationToken;
        var response = await client.PostAsJsonAsync("/labler", payload, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await mockRepo.Received(1).GetNewMessageIdsAsync(12345);
        await mockRepo.Received(1).GetEmailAsync("msg123");
    }

    [Fact]
    public async Task InvalidJsonInPayload_ReturnsBadRequest()
    {
        var mockRepo = Substitute.For<IEmailRepository>();

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.ContentRootKey, TestHelper.RepoRoot);
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IValidateOptions<EmailLabeler.Configuration.GmailConfig>>();
                    services.RemoveAll<IEmailRepository>();
                    services.RemoveAll<EmailLabeler.Adapters.IGmailRepository>();
                    services.AddScoped<IEmailRepository>(_ => mockRepo);
                });
            });

        using var client = factory.CreateClient();

        // Send valid base64 but invalid JSON that won't deserialize to GmailNotification
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes("not json"));
        var payload = new PubSubPushEnvelope(
            new PubSubMessagePayload(data, "mid-1", "2024-01-01T00:00:00Z"),
            "projects/test/subscriptions/gmail");

        var ct = TestContext.Current.CancellationToken;
        var response = await client.PostAsJsonAsync("/labler", payload, ct);

        // JSON deserialization of non-JSON returns null → BadRequest
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidBase64String_ReturnsBadRequest()
    {
        var mockRepo = Substitute.For<IEmailRepository>();

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.ContentRootKey, TestHelper.RepoRoot);
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IValidateOptions<EmailLabeler.Configuration.GmailConfig>>();
                    services.RemoveAll<IEmailRepository>();
                    services.RemoveAll<EmailLabeler.Adapters.IGmailRepository>();
                    services.AddScoped<IEmailRepository>(_ => mockRepo);
                });
            });

        using var client = factory.CreateClient();

        var payload = new PubSubPushEnvelope(
            new PubSubMessagePayload("!!!not-base64!!!", "mid-1", "2024-01-01T00:00:00Z"),
            "projects/test/subscriptions/gmail");

        var ct = TestContext.Current.CancellationToken;
        var response = await client.PostAsJsonAsync("/labler", payload, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmptyHistory_ReturnsOkWithNoProcessing()
    {
        var mockRepo = Substitute.For<IEmailRepository>();
        mockRepo.GetNewMessageIdsAsync(12345).Returns(Enumerable.Empty<string>());

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.ContentRootKey, TestHelper.RepoRoot);
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IValidateOptions<EmailLabeler.Configuration.GmailConfig>>();
                    services.RemoveAll<IEmailRepository>();
                    services.RemoveAll<EmailLabeler.Adapters.IGmailRepository>();
                    services.AddScoped<IEmailRepository>(_ => mockRepo);
                });
            });

        using var client = factory.CreateClient();

        var notification = new { emailAddress = "user@gmail.com", historyId = 12345 };
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(notification)));
        var payload = new PubSubPushEnvelope(
            new PubSubMessagePayload(data, "mid-1", "2024-01-01T00:00:00Z"),
            "projects/test/subscriptions/gmail");

        var ct = TestContext.Current.CancellationToken;
        var response = await client.PostAsJsonAsync("/labler", payload, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await mockRepo.Received(1).GetNewMessageIdsAsync(12345);
        await mockRepo.DidNotReceive().GetEmailAsync(Arg.Any<string>());
    }
}
