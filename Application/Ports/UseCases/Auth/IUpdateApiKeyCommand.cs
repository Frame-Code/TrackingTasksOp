using Application.Dto;
using Application.Dto.Auth;

namespace Application.Ports.UseCases.Auth;

public interface IUpdateApiKeyCommand
{
    Task<ResponseDto<AuthenticatedUserResponse>> ExecuteAsync(UpdateApiKeyRequest request, CancellationToken ct);
}
