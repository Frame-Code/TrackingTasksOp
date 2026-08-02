using Application.Dto.Conversation;

namespace Infrastructure.Adapters.Services.Bot;

/// <summary>
/// Encapsula la comunicación HTTP con la API de Groq: construcción del prompt
/// del sistema, historial de mensajes, llamada HTTP y limpieza de la respuesta.
/// </summary>
public interface IGroqApiClient
{
    /// <summary>
    /// Indica si hay una API Key de Groq configurada.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Solicita una respuesta al modelo de Groq para el prompt actual,
    /// incluyendo el historial de la conversación como contexto. Puede devolver
    /// texto plano, tool calls estructuradas (ver <see cref="GroqTools"/>), o ambos.
    /// </summary>
    Task<GroqCompletionResult> GetCompletionAsync(ConversationContext context, string prompt, CancellationToken ct = default);
}
