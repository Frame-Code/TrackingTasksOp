using Application.Dto;
using Application.Dto.Auth;

namespace Application.Ports.UseCases.Auth;

public interface IRegisterLocalUserCommand
{
    Task<ResponseDto<AuthenticatedUserResponse>> ExecuteAsync(LocalRegisterRequest request, CancellationToken ct);
}
