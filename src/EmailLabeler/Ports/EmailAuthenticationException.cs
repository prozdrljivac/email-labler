namespace EmailLabeler.Ports;

/// <summary>Exception thrown when the email provider rejects configured credentials.</summary>
public class EmailAuthenticationException : Exception
{
    /// <summary>Initializes a new instance of <see cref="EmailAuthenticationException"/>.</summary>
    public EmailAuthenticationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of <see cref="EmailAuthenticationException"/>.</summary>
    public EmailAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
