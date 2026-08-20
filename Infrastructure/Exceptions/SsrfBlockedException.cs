namespace Infrastructure.Exceptions;

public class SsrfBlockedException : InvalidOperationException
{
    public SsrfBlockedException(string message) : base(message)
    {
    }
}
