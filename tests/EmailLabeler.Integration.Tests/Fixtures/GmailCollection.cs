using Xunit;

namespace EmailLabeler.Integration.Tests.Fixtures;

[CollectionDefinition(Name)]
public class GmailCollection : ICollectionFixture<WireMockGmailFixture>
{
    public const string Name = "Gmail";
}
