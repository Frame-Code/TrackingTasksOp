namespace Application.Ports.UseCases.Auth;

public interface IRevokeOAuthSessionCommand
{
    Task Execute(CancellationToken ct = default);
}
