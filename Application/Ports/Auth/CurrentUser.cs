namespace Application.Ports.Auth;

public abstract class CurrentUser
{
    public abstract string? UserId { get; }
    public abstract bool IsAuthenticated { get; }
    public abstract string? OpenProjectInstanceUrl { get; }
}