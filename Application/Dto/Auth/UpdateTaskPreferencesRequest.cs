namespace Application.Dto.Auth;

/// <summary>DefaultStatusIds vacío = sin filtro por defecto al cargar tareas.</summary>
public record UpdateTaskPreferencesRequest(
    string PauseDefaultBehavior,
    bool SkipCancelConfirmation,
    bool AddRandomSlackTime,
    List<int> DefaultStatusIds);
