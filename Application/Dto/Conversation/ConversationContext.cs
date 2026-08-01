namespace Application.Dto.Conversation;

/// <summary>
/// Data Transfer Object que representa el estado y el historial de una conversación.
/// </summary>
public class ConversationContext
{
    public string SessionId { get; set; } = string.Empty;
    public List<HistoryItem> History { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Proyecto al que pertenecen los <see cref="PendingCustomFields"/> guardados.
    /// Evita arrastrar valores de un borrador de tarea hacia otro proyecto distinto.
    /// </summary>
    public int? PendingTaskProjectId { get; set; }

    /// <summary>
    /// Campos personalizados (nombre -> valor) ya resueltos de un "start_task" que quedó
    /// esperando datos adicionales. Se persiste aquí (y por lo tanto en Redis, como el resto
    /// del contexto) para no depender de que el LLM los recuerde y los reenvíe en el JSON.
    /// </summary>
    public Dictionary<string, string>? PendingCustomFields { get; set; }

    /// <summary>
    /// Datos base (proyecto, nombre, fechas, responsable, etc.) de un "start_task" que quedó
    /// esperando campos personalizados. El LLM no necesita repetirlos en el siguiente turno:
    /// si el JSON no los trae, el handler los recupera de acá.
    /// </summary>
    public PendingStartTaskDraft? PendingStartTaskDraft { get; set; }

    public void AddUserMessage(string message)
    {
        History.Add(new HistoryItem { Type = "user", Content = message, Timestamp = DateTime.UtcNow });
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void AddTtmMessage(string message)
    {
        History.Add(new HistoryItem { Type = "ttm", Content = message, Timestamp = DateTime.UtcNow });
        LastUpdatedAt = DateTime.UtcNow;
    }
}

public class HistoryItem
{
    public string Type { get; set; } = string.Empty; // "user" o "ttm" (TrackingTasksOp Model)
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Borrador de los datos base de "start_task" ya resueltos, pendientes de campos personalizados.
/// </summary>
public class PendingStartTaskDraft
{
    public int ProjectId { get; set; }
    public int StatusId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? AssigneeId { get; set; }
    public int? ResponsibleId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
}
