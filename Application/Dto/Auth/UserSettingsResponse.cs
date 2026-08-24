namespace Application.Dto.Auth;

public class UserSettingsResponse
{
    public List<NotificationSettingDto> Notifications { get; init; } = [];
    public string? OpenProjectInstanceUrl { get; init; }
    public string Email { get; init; } = null!;
    public string PauseDefaultBehavior { get; init; } = null!;
    public bool SkipCancelConfirmation { get; init; }
    public bool AddRandomSlackTime { get; init; }

    /// <summary>Estados que se aplican como filtro apenas carga "Cargar tareas". Vacío = todos.</summary>
    public List<int> DefaultStatusIds { get; init; } = [];

    /// <summary>true = usa su propia key de Groq (sin límite diario); false = key compartida (con límite).</summary>
    public bool HasCustomAiApiKey { get; init; }

    /// <summary>true = admin en OpenProject; habilita acciones como conectar OAuth para la organización.</summary>
    public bool IsAdmin { get; init; }
}
