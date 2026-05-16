namespace Infrastructure.Exceptions;

public class InitializerInstanceException : InvalidOperationException
{
    public InitializerInstanceException(string message) : base(message)
    {
    }
}