using Application.Dto;
using Application.Dto.Auth;

namespace Application.Ports.UseCases.Auth;

public interface ILoginLocalUserCommand
{
    Task<ResponseDto<AuthenticatedUserResponse>> ExecuteAsync(LocalLoginRequest request, CancellationToken ct);
}