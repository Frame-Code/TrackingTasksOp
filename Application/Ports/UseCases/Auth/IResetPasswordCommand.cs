using Application.Dto.Auth;

namespace Application.Ports.UseCases.Auth;

public interface IResetPasswordCommand
{
    Task ExecuteAsync(ResetPasswordRequest request, CancellationToken ct = default);
}
