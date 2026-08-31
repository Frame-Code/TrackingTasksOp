namespace Application.Dto.Auth;

/// <summary>
/// Resetea la contraseña de otro usuario de la misma instancia OpenProject. Lo ejecuta un
/// admin cuando alguien perdió el acceso y no puede pasar por el cambio de contraseña normal
/// (que exige la contraseña actual). No hay flujo de auto-recuperación por correo porque el
/// proyecto no tiene envío de mail: la contraseña nueva se la pasa el admin por fuera.
/// </summary>
public record AdminResetPasswordRequest(string Email, string NewPassword);
