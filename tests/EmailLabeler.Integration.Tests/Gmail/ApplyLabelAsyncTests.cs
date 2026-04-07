using EmailLabeler.Integration.Tests.Fixtures;
using WireMock.Admin.Mappings;
using WireMock.Admin.Requests;
using Xunit;

namespace EmailLabeler.Integration.Tests.Gmail;

[Collection(GmailCollection.Name)]
public class ApplyLabelAsyncTests
{
    private readonly WireMockGmailFixture _fixture;

    public ApplyLabelAsyncTests(WireMockGmailFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task LabelExists_AppliesWithoutCreating()
    {
        var ct = TestContext.Current.CancellationToken;
        await _fixture.AdminApi.ResetMappingsAsync(null, ct);
        await _fixture.AdminApi.DeleteRequestsAsync(ct);

        // Stub labels.list — label already exists
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

        // Stub messages.modify
        await _fixture.AdminApi.PostMappingAsync(new MappingModel
        {
            Request = new RequestModel { Methods = ["POST"], Path = "/gmail/v1/users/me/messages/msg123/modify" },
            Response = new ResponseModel
            {
                StatusCode = 200,
                BodyAsJson = new { id = "msg123", labelIds = new[] { "Label_1" } }
            }
        }, ct);

        var repo = GmailRepositoryFactory.Create(_fixture.BaseUrl);

        await repo.EnsureLabelExistsAsync("Newsletters");
        await repo.ApplyLabelAsync("msg123", "Newsletters");

        // Assert labels.create was NOT called
        var requests = await _fixture.AdminApi.GetRequestsAsync(ct);
        Assert.DoesNotContain(requests,
            r => r.Request!.Method == "POST" && r.Request.Url!.Contains("/gmail/v1/users/me/labels"));

        // Assert messages.modify WAS called with correct label ID
        var modifyRequest = Assert.Single(requests,
            r => r.Request!.Method == "POST" && r.Request.Url!.Contains("/messages/msg123/modify"));
        Assert.Contains("Label_1", modifyRequest.Request!.Body);
    }

    [Fact]
    public async Task LabelDoesNotExist_CreatesAndApplies()
    {
        var ct = TestContext.Current.CancellationToken;
        await _fixture.AdminApi.ResetMappingsAsync(null, ct);
        await _fixture.AdminApi.DeleteRequestsAsync(ct);

        // Stub labels.list — empty
        await _fixture.AdminApi.PostMappingAsync(new MappingModel
        {
            Request = new RequestModel { Methods = ["GET"], Path = "/gmail/v1/users/me/labels" },
            Response = new ResponseModel
            {
                StatusCode = 200,
                BodyAsJson = new { labels = Array.Empty<object>() }
            }
        }, ct);

        // Stub labels.create
        await _fixture.AdminApi.PostMappingAsync(new MappingModel
        {
            Request = new RequestModel { Methods = ["POST"], Path = "/gmail/v1/users/me/labels" },
            Response = new ResponseModel
            {
                StatusCode = 200,
                BodyAsJson = new { id = "Label_new", name = "Newsletters" }
            }
        }, ct);

        // Stub messages.modify
        await _fixture.AdminApi.PostMappingAsync(new MappingModel
        {
            Request = new RequestModel { Methods = ["POST"], Path = "/gmail/v1/users/me/messages/msg123/modify" },
            Response = new ResponseModel
            {
                StatusCode = 200,
                BodyAsJson = new { id = "msg123", labelIds = new[] { "Label_new" } }
            }
        }, ct);

        var repo = GmailRepositoryFactory.Create(_fixture.BaseUrl);

        await repo.ApplyLabelAsync("msg123", "Newsletters");

        var requests = await _fixture.AdminApi.GetRequestsAsync(ct);

        // Assert labels.create WAS called
        var createRequest = Assert.Single(requests,
            r => r.Request!.Method == "POST" && r.Request.Url!.Contains("/gmail/v1/users/me/labels")
                 && !r.Request.Url.Contains("/messages/"));
        Assert.Contains("Newsletters", createRequest.Request!.Body);

        // Assert messages.modify called with new label ID
        var modifyRequest = Assert.Single(requests,
            r => r.Request!.Method == "POST" && r.Request.Url!.Contains("/messages/msg123/modify"));
        Assert.Contains("Label_new", modifyRequest.Request!.Body);
    }
}
