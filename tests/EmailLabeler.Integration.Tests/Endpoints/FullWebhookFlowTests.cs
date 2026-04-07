using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EmailLabeler.Integration.Tests.Fixtures;
using EmailLabeler.Endpoints;
using WireMock.Admin.Mappings;
using Xunit;

namespace EmailLabeler.Integration.Tests.Endpoints;

[Collection(GmailCollection.Name)]
public class FullWebhookFlowTests
{
    private readonly WireMockGmailFixture _fixture;

    public FullWebhookFlowTests(WireMockGmailFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task LablerPayload_ProcessesEmailAndAppliesLabel()
    {
        var ct = TestContext.Current.CancellationToken;
        await _fixture.AdminApi.ResetMappingsAsync(null, ct);
        await _fixture.AdminApi.DeleteRequestsAsync(ct);

        // Stub history.list — returns messagesAdded with msg123
        await _fixture.AdminApi.PostMappingAsync(new MappingModel
        {
            Request = new RequestModel { Methods = ["GET"], Path = "/gmail/v1/users/me/history" },
            Response = new ResponseModel
            {
                StatusCode = 200,
                BodyAsJson = new
                {
                    history = new[]
                    {
                        new
                        {
                            messagesAdded = new[]
                            {
                                new { message = new { id = "msg123" } }
                            }
                        }
                    }
                }
            }
        }, ct);

        // Stub messages.get — email from sender@newsletter.com
        await _fixture.AdminApi.PostMappingAsync(new MappingModel
        {
            Request = new RequestModel { Methods = ["GET"], Path = "/gmail/v1/users/me/messages/msg123" },
            Response = new ResponseModel
            {
                StatusCode = 200,
                BodyAsJson = new
                {
                    id = "msg123",
                    threadId = "thread123",
                    labelIds = new[] { "INBOX", "UNREAD" },
                    payload = new
                    {
                        headers = new[]
                        {
                            new { name = "From", value = "sender@newsletter.com" },
                            new { name = "Subject", value = "Weekly digest" }
                        }
                    }
                }
            }
        }, ct);

        // Stub labels.list — contains Newsletters label
        await _fixture.AdminApi.PostMappingAsync(new MappingModel
        {
            Request = new RequestModel { Methods = ["GET"], Path = "/gmail/v1/users/me/labels" },
            Response = new ResponseModel
            {
                StatusCode = 200,
                BodyAsJson = new
                {
                    labels = new[]
                    {
                        new { id = "Label_1", name = "Newsletters" }
                    }
                }
            }
        }, ct);

        // Stub messages.modify — 200 OK
        await _fixture.AdminApi.PostMappingAsync(new MappingModel
        {
            Request = new RequestModel { Methods = ["POST"], Path = "/gmail/v1/users/me/messages/msg123/modify" },
            Response = new ResponseModel
            {
                StatusCode = 200,
                BodyAsJson = new { id = "msg123", labelIds = new[] { "Label_1" } }
            }
        }, ct);

        await using var factory = new CustomWebApplicationFactory
        {
            WireMockBaseUrl = _fixture.BaseUrl
        };
        using var client = factory.CreateClient();

        // Build Pub/Sub payload
        var notification = new { emailAddress = "user@gmail.com", historyId = 12345 };
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(notification)));
        var payload = new PubSubPushEnvelope(
            new PubSubMessagePayload(data, "mid-1", "2024-01-01T00:00:00Z"),
            "projects/test/subscriptions/gmail");

        var request = new HttpRequestMessage(HttpMethod.Post, "/labler");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        request.Content = JsonContent.Create(payload);

        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify messages.modify was called with the correct label ID
        var requests = await _fixture.AdminApi.GetRequestsAsync(ct);
        var modifyRequest = Assert.Single(requests,
            r => r.Request!.Method == "POST" && r.Request!.Url!.Contains("/messages/msg123/modify"));
        Assert.Contains("Label_1", modifyRequest.Request!.Body);
        Assert.Contains("addLabelIds", modifyRequest.Request!.Body);
    }

    [Fact]
    public async Task NoMatchingRules_ReturnsOkWithoutModify()
    {
        var ct = TestContext.Current.CancellationToken;
        await _fixture.AdminApi.ResetMappingsAsync(null, ct);
        await _fixture.AdminApi.DeleteRequestsAsync(ct);

        // Stub history.list — returns messagesAdded with msg456
        await _fixture.AdminApi.PostMappingAsync(new MappingModel
        {
            Request = new RequestModel { Methods = ["GET"], Path = "/gmail/v1/users/me/history" },
            Response = new ResponseModel
            {
                StatusCode = 200,
                BodyAsJson = new
                {
                    history = new[]
                    {
                        new
                        {
                            messagesAdded = new[]
                            {
                                new { message = new { id = "msg456" } }
                            }
                        }
                    }
                }
            }
        }, ct);

        // Stub messages.get — email from unknown@other.com (doesn't match @newsletter.com rule)
        await _fixture.AdminApi.PostMappingAsync(new MappingModel
        {
            Request = new RequestModel { Methods = ["GET"], Path = "/gmail/v1/users/me/messages/msg456" },
            Response = new ResponseModel
            {
                StatusCode = 200,
                BodyAsJson = new
                {
                    id = "msg456",
                    threadId = "thread456",
                    labelIds = new[] { "INBOX", "UNREAD" },
                    payload = new
                    {
                        headers = new[]
                        {
                            new { name = "From", value = "unknown@other.com" },
                            new { name = "Subject", value = "Random email" }
                        }
                    }
                }
            }
        }, ct);

        await using var factory = new CustomWebApplicationFactory
        {
            WireMockBaseUrl = _fixture.BaseUrl
        };
        using var client = factory.CreateClient();

        var notification = new { emailAddress = "user@gmail.com", historyId = 99999 };
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(notification)));
        var payload = new PubSubPushEnvelope(
            new PubSubMessagePayload(data, "mid-2", "2024-01-01T00:00:00Z"),
            "projects/test/subscriptions/gmail");

        var request = new HttpRequestMessage(HttpMethod.Post, "/labler");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        request.Content = JsonContent.Create(payload);

        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify no messages.modify call was made
        var requests = await _fixture.AdminApi.GetRequestsAsync(ct);
        Assert.DoesNotContain(requests,
            r => r.Request!.Method == "POST" && r.Request!.Url!.Contains("/modify"));
    }
}
