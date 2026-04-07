using EmailLabeler.Adapters;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmailLabeler.Integration.Tests.Fixtures;

public static class GmailRepositoryFactory
{
    public static GmailRepository Create(string wireMockBaseUrl, string topicName = "projects/test/topics/gmail")
    {
        var gmailService = new GmailService(new BaseClientService.Initializer
        {
            BaseUri = wireMockBaseUrl.TrimEnd('/') + "/",
            ApplicationName = "EmailLabeler-Test"
        });

        return new GmailRepository(
            gmailService,
            userId: "me",
            topicName: topicName,
            logger: NullLogger<GmailRepository>.Instance);
    }
}
