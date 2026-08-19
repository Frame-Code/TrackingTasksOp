namespace Application.Dto.Auth;

public record UpdateNotificationSettingRequest(string TypeCode, bool Enabled, int IntervalMinutes);
