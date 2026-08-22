using Application.Dto.Auth;

namespace Application.Ports.UseCases.Account;

public interface IRegenerateRecoveryCodesCommand
{
    /// <summary>
    /// Emite una tanda nueva de códigos de recuperación e invalida la anterior. Disponible en
    /// cualquier momento: es lo que hace que los códigos no sean un secreto irrecuperable — si
    /// el usuario los perdió o no está seguro de dónde quedaron, genera otros y listo.
    /// Pide el código de la app para que no alcance con robar una sesión abierta.
    /// </summary>
    Task<RecoveryCodesResponse> Execute(EnableTwoFactorRequest request, CancellationToken ct = default);
}
