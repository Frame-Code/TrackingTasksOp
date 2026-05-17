namespace Infrastructure.Exceptions;

public class OpenProjectRequestException : InvalidOperationException
{
    public OpenProjectRequestException(string message) : base(message)
    {
    }
}