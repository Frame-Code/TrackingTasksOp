using Application.Dto.Auth;

namespace Application.Ports.UseCases.Auth;

public interface IRegisterLocalUserCommand
{
    Task<AuthenticatedUserResponse> ExecuteAsync(LocalRegisterRequest request, CancellationToken ct);
}
