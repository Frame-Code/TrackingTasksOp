namespace Application.Dto.Auth;

public record NotificationSettingDto(string TypeCode, bool Enabled, int IntervalMinutes);
