using Application.Dto.Conversation;

namespace Infrastructure.Adapters.Services.Bot;

/// <summary>
/// Parsea bloques JSON de acciones devueltos por el LLM y delega su ejecución
/// al <see cref="IBotActionHandler"/> correspondiente.
/// </summary>
public interface IBotActionExecutor
{
    /// <summary>
    /// Deserializa y ejecuta cada bloque JSON de acción, en orden, devolviendo
    /// el mensaje de resultado de cada una.
    /// </summary>
    /// <param name="conversationContext">Contexto de la conversación actual; permite a los handlers persistir estado entre turnos (ej. campos de tarea ya resueltos).</param>
    Task<List<string>> ExecuteAllAsync(IEnumerable<string> jsonBlocks, ConversationContext conversationContext, CancellationToken ct = default);
}
