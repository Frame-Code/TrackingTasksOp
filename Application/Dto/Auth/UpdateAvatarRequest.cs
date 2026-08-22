namespace Application.Dto.Auth;

/// <summary>
/// Avatar en base64, sin el prefijo "data:image/jpeg;base64,". El navegador ya lo redimensionó
/// a 256px y lo exportó como JPEG antes de mandarlo.
///
/// Null o vacío = quitar el avatar y volver a las iniciales, mismo criterio que
/// <see cref="UpdateAiApiKeyRequest"/>.
/// </summary>
public record UpdateAvatarRequest(string? JpegBase64);
