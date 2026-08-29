namespace Infrastructure.Exceptions;

public class InvalidApiKeyException : InvalidOperationException
{
    public InvalidApiKeyException(string message) : base(message)
    {
    }

    public InvalidApiKeyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
