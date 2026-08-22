namespace Infrastructure.DataAccess.Entities;

/// <summary>
/// Avatar del usuario, siempre JPEG. Ausencia de fila = se muestran las iniciales
/// (comportamiento actual del sidebar).
///
/// Tabla aparte y no una columna en ApplicationUser a propósito: FindByIdAsync corre en
/// caliente — AiUsageLimiterImpl y GroqAuthHeaderProvider lo llaman en cada mensaje del bot —
/// y EF traería los bytes de la imagen en todas esas consultas.
///
/// ponytail: se guarda en la base y no en disco. Así viaja con el dump al migrar al VPS y no
/// suma otro volumen que declarar en el container (y que olvidar en la mudanza).
/// </summary>
public class UserAvatar
{
    public string UserId { get; set; } = null!;

    /// <summary>Imagen JPEG, ya redimensionada a 256px por el navegador antes de subir (~15KB).</summary>
    public byte[] Jpeg { get; set; } = null!;

    /// <summary>Alimenta el ETag de GET avatar, para que el navegador no lo baje en cada carga.</summary>
    public DateTime UpdatedAt { get; set; }
}
