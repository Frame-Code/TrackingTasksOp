namespace Application.Dto.Auth;

/// <summary>
/// Desvincula la app de autenticación actual para poder enrolar otra (teléfono nuevo, perdido
/// o robado). Deja el 2FA desactivado, así que después hay que volver a pasar por el setup.
/// </summary>
/// <param name="TwoFactorCode">
/// El código de la app si todavía se tiene acceso a ella, o uno de recuperación si no. Esto
/// último es el caso que importa: es la salida cuando el teléfono ya no está.
/// </param>
public record ResetAuthenticatorRequest(string CurrentPassword, string TwoFactorCode);
