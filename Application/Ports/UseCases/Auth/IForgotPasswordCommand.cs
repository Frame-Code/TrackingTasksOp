using Application.Dto.Auth;

namespace Application.Ports.UseCases.Auth;

public interface IForgotPasswordCommand
{
    Task ExecuteAsync(ForgotPasswordRequest request, CancellationToken ct = default);
}
