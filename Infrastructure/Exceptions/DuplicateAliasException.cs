namespace Infrastructure.Exceptions;

public class DuplicateAliasException(string message) : Exception(message)
{
}
