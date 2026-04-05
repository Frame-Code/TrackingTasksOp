namespace Application.Ports.Services;

/// <summary>
/// Define el contrato para un servicio que interpreta la intención
/// del usuario utilizando un modelo de IA generativa como Gemini.
/// </summary>
public interface IGeminiIntentService
{
    /// <summary>
    /// Procesa el prompt de un usuario para determinar su intención.
    /// </summary>
    /// <param name="prompt">El texto ingresado por el usuario.</param>
    /// <param name="sessionId">El ID de la sesión actual para mantener contexto.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Una cadena con la respuesta o intención interpretada.</returns>
    Task<string> GetIntentAsync(string prompt, string sessionId, CancellationToken ct = default);
}
