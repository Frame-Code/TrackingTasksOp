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

    /// <summary>
    /// true = admin de esta app (rol local, no de OpenProject); habilita resetear la contraseña
    /// de otros usuarios. No hay UI para otorgarlo, ver Docs/Cuenta.md.
    /// </summary>
    public bool IsAppAdmin { get; init; }

    /// <summary>
    /// true = ya enroló la app de autenticación. La UI de "Mi cuenta" lo usa para decidir si el
    /// cambio de contraseña arranca por el enrolamiento o va directo al formulario.
    /// </summary>
    public bool TwoFactorEnabled { get; init; }

    /// <summary>true = tiene avatar propio; false = el sidebar muestra las iniciales.</summary>
    public bool HasAvatar { get; init; }
}
