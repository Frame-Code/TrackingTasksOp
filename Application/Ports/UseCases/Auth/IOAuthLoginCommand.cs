using Application.Dto;
using Application.Dto.Auth;

namespace Application.Ports.UseCases.Auth;

public interface IOAuthLoginCommand
{
    Task<ResponseDto<AuthenticatedUserResponse>> ExecuteAsync(string code, string state, CancellationToken ct);
}
