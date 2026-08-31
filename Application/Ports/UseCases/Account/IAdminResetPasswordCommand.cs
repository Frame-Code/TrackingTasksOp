using Application.Dto.Auth;

namespace Application.Ports.UseCases.Account;

public interface IAdminResetPasswordCommand
{
    /// <summary>
    /// Exige que el usuario autenticado sea admin de OpenProject y que el usuario objetivo
    /// pertenezca a la misma instancia. Falla si cualquiera de las dos condiciones no se cumple.
    /// </summary>
    Task Execute(AdminResetPasswordRequest request, CancellationToken ct = default);
}
