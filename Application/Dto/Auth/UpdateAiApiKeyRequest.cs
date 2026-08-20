namespace Application.Dto.Auth;

/// <summary>ApiKey null o vacío = quitar la key propia y volver a la compartida (con límite).</summary>
public record UpdateAiApiKeyRequest(string? ApiKey);
