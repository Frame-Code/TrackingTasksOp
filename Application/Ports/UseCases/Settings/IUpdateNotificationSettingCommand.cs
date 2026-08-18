using Application.Dto.Auth;

namespace Application.Ports.UseCases.Settings;

public interface IUpdateNotificationSettingCommand
{
    Task Execute(UpdateNotificationSettingRequest request, CancellationToken ct = default);
}
