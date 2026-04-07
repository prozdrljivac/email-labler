using EmailLabeler.Integration.Tests.Fixtures;
using WireMock.Admin.Mappings;
using Xunit;

namespace EmailLabeler.Integration.Tests.Gmail;

[Collection(GmailCollection.Name)]
public class ArchiveAsyncTests
{
    private readonly WireMockGmailFixture _fixture;

    public ArchiveAsyncTests(WireMockGmailFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ArchiveAsync_RemovesInboxLabel()
    {
        var ct = TestContext.Current.CancellationToken;
        await _fixture.AdminApi.ResetMappingsAsync(null, ct);
        await _fixture.AdminApi.DeleteRequestsAsync(ct);

        await _fixture.AdminApi.PostMappingAsync(new MappingModel
        {
            Request = new RequestModel { Methods = ["POST"], Path = "/gmail/v1/users/me/messages/msg123/modify" },
            Response = new ResponseModel
            {
                StatusCode = 200,
                BodyAsJson = new { id = "msg123", labelIds = Array.Empty<string>() }
            }
        }, ct);

        var repo = GmailRepositoryFactory.Create(_fixture.BaseUrl);

        await repo.ArchiveAsync("msg123");

        var requests = await _fixture.AdminApi.GetRequestsAsync(ct);
        var modifyRequest = Assert.Single(requests);
        Assert.Contains("INBOX", modifyRequest.Request!.Body);
        Assert.Contains("removeLabelIds", modifyRequest.Request.Body);
    }
}
