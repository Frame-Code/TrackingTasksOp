namespace Application.Dto.Auth;

public record UpdateTaskPreferencesRequest(string PauseDefaultBehavior, bool SkipCancelConfirmation, bool AddRandomSlackTime);
