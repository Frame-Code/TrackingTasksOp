namespace Application.Dto.Auth;

/// <summary>
/// Cambio de contraseña con segunda verificación.
/// </summary>
/// <param name="TwoFactorCode">
/// Acepta tanto el código de 6 dígitos de la app de autenticación como uno de los códigos de
/// recuperación. Si solo valiera el primero, perder el teléfono dejaría al usuario sin forma
/// de cambiar su contraseña.
/// </param>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string TwoFactorCode);
