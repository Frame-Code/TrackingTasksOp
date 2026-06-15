namespace Infrastructure.Adapters.Services.Bot;

/// <summary>
/// Capa de interceptación heurística: resuelve intenciones simples y muy frecuentes
/// (listar proyectos, tareas pendientes, estados) sin consultar al LLM.
/// </summary>
public interface IBotIntentInterceptor
{
    /// <summary>
    /// Normaliza un prompt de usuario (minúsculas, sin acentos/diacríticos, recortado).
    /// </summary>
    string Normalize(string prompt);

    /// <summary>
    /// Intenta resolver el prompt normalizado con una respuesta directa.
    /// Devuelve null si no aplica ninguna heurística (debe pasar al LLM).
    /// </summary>
    Task<string?> TryInterceptAsync(string normalizedPrompt, CancellationToken ct = default);
}
