using EmailLabeler.Ports;
using Xunit;

namespace EmailLabeler.Unit.Tests.Ports;

public class EmailAuthenticationExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var exception = new EmailAuthenticationException("Credentials rejected");

        Assert.Equal("Credentials rejected", exception.Message);
    }

    [Fact]
    public void Constructor_WithInnerException_SetsMessageAndInnerException()
    {
        var inner = new InvalidOperationException("Token expired");

        var exception = new EmailAuthenticationException("Credentials rejected", inner);

        Assert.Equal("Credentials rejected", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }
}
