using EmailLabeler.Integration.Tests.Fixtures;
using WireMock.Admin.Mappings;
using Xunit;

namespace EmailLabeler.Integration.Tests.Gmail;

[Collection(GmailCollection.Name)]
public class GetEmailAsyncTests
{
    private readonly WireMockGmailFixture _fixture;

    public GetEmailAsyncTests(WireMockGmailFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetEmailAsync_ReturnsEmailWithHeadersAndLabels()
    {
        var ct = TestContext.Current.CancellationToken;
        await _fixture.AdminApi.ResetMappingsAsync(null, ct);
        await _fixture.AdminApi.DeleteRequestsAsync(ct);

        await _fixture.AdminApi.PostMappingAsync(new MappingModel
        {
            Request = new RequestModel
            {
                Methods = ["GET"],
                Path = "/gmail/v1/users/me/messages/msg123"
            },
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

        var repo = GmailRepositoryFactory.Create(_fixture.BaseUrl);

        var email = await repo.GetEmailAsync("msg123");

        Assert.Equal("msg123", email.Id);
        Assert.Equal("sender@newsletter.com", email.From);
        Assert.Equal("Weekly digest", email.Subject);
        Assert.Contains("INBOX", email.LabelIds);
        Assert.Contains("UNREAD", email.LabelIds);
    }
}
