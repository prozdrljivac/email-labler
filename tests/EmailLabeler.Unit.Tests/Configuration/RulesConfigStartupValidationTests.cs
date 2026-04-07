using System.Text;
using EmailLabeler.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace EmailLabeler.Unit.Tests.Configuration;

public class RulesConfigStartupValidationTests
{
    [Fact]
    public void InvalidConfig_ThrowsOptionsValidationException()
    {
        const string yaml = "rules:\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
        var config = new ConfigurationBuilder()
            .AddYamlStream(stream)
            .Build();

        var services = new ServiceCollection();
        services.AddRulesConfig(config);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RulesConfig>>();

        var exception = Assert.Throws<OptionsValidationException>(() => options.Value);
        Assert.Contains("At least one rule must be defined", exception.Message);
    }
}
