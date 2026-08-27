using System.Net;
using System.Text.RegularExpressions;

namespace Infrastructure.Adapters.Services.Bot;

/// <summary>
/// Motivo del fallo, ya clasificado. Se decide una sola vez acá y no leyendo el cuerpo
/// crudo en cada lugar que lo necesite: el cliente lo usa para decidir si reintenta y el
/// orquestador para elegir qué mensaje ve el usuario.
/// </summary>
public enum GroqFailureKind
{
    /// <summary>Fallo no reconocido. No se reintenta.</summary>
    Unknown,

    /// <summary>Se pasó el límite de tokens por minuto del plan. Transitorio: se reintenta.</summary>
    RateLimited,

    /// <summary>
    /// El modelo intentó llamar una función que no va declarada en "tools". Pasa porque el
    /// system prompt le enseña acciones que viajan como JSON embebido, no como tool nativa.
    /// </summary>
    ToolValidation,

    /// <summary>API key ausente, vencida o sin permisos.</summary>
    Authentication
}

/// <summary>
/// Error devuelto por la API de Groq. El cuerpo crudo queda acá para el log y NUNCA se
/// muestra al usuario: trae JSON, nombres de modelo e IDs de organización.
/// </summary>
public class GroqApiException(HttpStatusCode statusCode, string body, GroqFailureKind kind, TimeSpan? retryAfter)
    : Exception($"Groq API error ({statusCode}): {body}")
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    /// <summary>Cuerpo de la respuesta. Solo para logs.</summary>
    public string Body { get; } = body;

    public GroqFailureKind Kind { get; } = kind;

    /// <summary>Espera sugerida por Groq antes de reintentar, si la indicó.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;

    public static GroqApiException FromResponse(HttpStatusCode statusCode, string body)
    {
        var kind = statusCode switch
        {
            HttpStatusCode.TooManyRequests => GroqFailureKind.RateLimited,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => GroqFailureKind.Authentication,
            HttpStatusCode.BadRequest when body.Contains("tool call validation failed", StringComparison.OrdinalIgnoreCase)
                => GroqFailureKind.ToolValidation,
            _ => GroqFailureKind.Unknown
        };

        return new GroqApiException(statusCode, body, kind, ParseRetryAfter(body));
    }

    /// <summary>
    /// Groq mete la espera en el texto del error ("Please try again in 5.025s"), no en una
    /// cabecera. Si cambia el formato devolvemos null y simplemente no se reintenta.
    /// </summary>
    private static TimeSpan? ParseRetryAfter(string body)
    {
        var match = Regex.Match(body, @"try again in (\d+(?:\.\d+)?)s", RegexOptions.IgnoreCase);

        return match.Success
               && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                                  System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : null;
    }
}
