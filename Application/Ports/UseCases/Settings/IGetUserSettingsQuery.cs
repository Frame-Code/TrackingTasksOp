using Application.Dto.Auth;

namespace Application.Ports.UseCases.Settings;

public interface IGetUserSettingsQuery
{
    Task<UserSettingsResponse> Execute(CancellationToken ct = default);
}
