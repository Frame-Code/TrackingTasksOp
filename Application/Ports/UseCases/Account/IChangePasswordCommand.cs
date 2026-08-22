using Application.Dto.Auth;

namespace Application.Ports.UseCases.Account;

public interface IChangePasswordCommand
{
    /// <summary>
    /// Cambia la contraseña exigiendo segunda verificación. Falla si el usuario todavía no
    /// activó el 2FA: la UI tiene que mandarlo primero por el enrolamiento.
    /// </summary>
    Task Execute(ChangePasswordRequest request, CancellationToken ct = default);
}
