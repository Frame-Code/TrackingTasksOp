namespace Application.Dto.Auth;

/// <summary>
/// Datos para enrolar la app de autenticación. Se devuelven los dos juntos a propósito:
/// el QR para el caso normal, y la clave manual para cuando la cámara no coopera.
/// </summary>
/// <param name="QrCodeDataUri">PNG embebido (data:image/png;base64,...), listo para un &lt;img src&gt;.</param>
/// <param name="ManualKey">La misma clave en base32, agrupada de a 4 para poder tipearla.</param>
public record TwoFactorSetupResponse(string QrCodeDataUri, string ManualKey);
