namespace Application.Dto.Conversation;

/// <summary>Resumen de un chat para el listado lateral del bot.</summary>
public record ConversationSummary(string SessionId, string Title, DateTime LastUpdatedAt);
