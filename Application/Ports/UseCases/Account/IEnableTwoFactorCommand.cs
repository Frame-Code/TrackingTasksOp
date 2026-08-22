using Application.Dto.Auth;

namespace Application.Ports.UseCases.Account;

public interface IEnableTwoFactorCommand
{
    /// <summary>
    /// Valida el código contra la clave generada en el setup y, si coincide, activa el 2FA y
    /// devuelve la primera tanda de códigos de recuperación. No se pueden volver a consultar,
    /// pero sí regenerar cuando el usuario quiera con <see cref="IRegenerateRecoveryCodesCommand"/>.
    /// </summary>
    Task<RecoveryCodesResponse> Execute(EnableTwoFactorRequest request, CancellationToken ct = default);
}
