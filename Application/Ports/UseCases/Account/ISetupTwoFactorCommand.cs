using Application.Dto.Auth;

namespace Application.Ports.UseCases.Account;

public interface ISetupTwoFactorCommand
{
    /// <summary>
    /// Genera (o recupera) la clave del authenticator del usuario y la devuelve como QR y como
    /// texto. No activa nada todavía: el 2FA recién queda activo cuando el usuario confirma un
    /// código con <see cref="IEnableTwoFactorCommand"/>. Así nadie queda con el 2FA prendido y
    /// una app mal sincronizada.
    /// </summary>
    Task<TwoFactorSetupResponse> Execute(CancellationToken ct = default);
}
