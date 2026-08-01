using Application.Dto.Conversation;

namespace Infrastructure.Adapters.Services.Bot;

/// <summary>
/// Estrategia para ejecutar una acción concreta solicitada por el LLM (Strategy/OCP).
/// </summary>
public interface IBotActionHandler
{
    /// <summary>
    /// Nombre de la acción que maneja (ej. "list_tasks", "start_task"). Se compara sin distinguir mayúsculas/minúsculas.
    /// </summary>
    string ActionName { get; }

    /// <summary>
    /// Ejecuta la acción y devuelve el mensaje de resultado para el usuario.
    /// </summary>
    /// <param name="contextWpId">ID de work package creado por una acción previa en el mismo turno, usado como fallback.</param>
    /// <param name="conversationContext">Contexto de la conversación; la mayoría de los handlers lo ignoran, solo lo usan los que necesitan recordar estado entre turnos (ej. "start_task").</param>
    Task<string> ExecuteAsync(GroqAction action, int? contextWpId, ConversationContext? conversationContext = null, CancellationToken ct = default);
}
