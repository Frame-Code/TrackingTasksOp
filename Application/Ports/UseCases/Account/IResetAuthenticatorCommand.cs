using Application.Dto.Auth;

namespace Application.Ports.UseCases.Account;

public interface IResetAuthenticatorCommand
{
    /// <summary>
    /// Desvincula la app de autenticación y deja el 2FA desactivado, para que el usuario pueda
    /// enrolar otro teléfono desde el setup. Es la salida del caso "cambié/perdí el celular":
    /// sin esto, quedarse sin el dispositivo dejaba la cuenta trabada aunque el usuario tuviera
    /// sesión abierta y supiera su contraseña.
    /// </summary>
    Task Execute(ResetAuthenticatorRequest request, CancellationToken ct = default);
}
