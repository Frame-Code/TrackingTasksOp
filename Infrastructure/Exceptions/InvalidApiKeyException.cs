namespace Infrastructure.Exceptions;

public class InvalidApiKeyException : InvalidOperationException
{
    public InvalidApiKeyException(string message) : base(message)
    {
    }
}