namespace Application.Dto.Auth;

public class UserSettingsResponse
{
    public List<NotificationSettingDto> Notifications { get; init; } = [];
    public string? OpenProjectInstanceUrl { get; init; }
    public string Email { get; init; } = null!;
    public string PauseDefaultBehavior { get; init; } = null!;
    public bool SkipCancelConfirmation { get; init; }
    public bool AddRandomSlackTime { get; init; }
}
