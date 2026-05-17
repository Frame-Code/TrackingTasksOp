using Application.Dto.Conversation;

namespace Application.Ports.Services;

/// <summary>
/// Define el contrato para un servicio que gestiona el estado
/// de la conversación a través de sesiones.
/// </summary>
public interface IConversationContextService
{
    /// <summary>
    /// Recupera el contexto de una conversación existente o crea uno nuevo.
    /// </summary>
    /// <param name="sessionId">El identificador único de la sesión.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>El contexto de la conversación.</returns>
    Task<ConversationContext> GetOrCreateAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Guarda el estado actual del contexto de la conversación.
    /// </summary>
    /// <param name="context">El contexto a guardar.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task SaveAsync(ConversationContext context, CancellationToken ct = default);

    /// <summary>
    /// Elimina el contexto de una conversación.
    /// </summary>
    /// <param name="sessionId">El identificador de la sesión a eliminar.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task DeleteAsync(string sessionId, CancellationToken ct = default);
}
