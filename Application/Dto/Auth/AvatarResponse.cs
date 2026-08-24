namespace Application.Dto.Auth;

/// <summary>
/// Avatar listo para servir. Existe como DTO y no se devuelve la entidad UserAvatar porque
/// esa vive en Infrastructure, y Application no puede referenciarla.
/// </summary>
/// <param name="UpdatedAt">Alimenta el ETag, para que el navegador no rebaje la imagen en cada carga.</param>
public record AvatarResponse(byte[] Jpeg, DateTime UpdatedAt);
