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
