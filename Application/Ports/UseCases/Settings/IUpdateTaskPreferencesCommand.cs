using Application.Dto.Auth;

namespace Application.Ports.UseCases.Settings;

public interface IUpdateTaskPreferencesCommand
{
    Task Execute(UpdateTaskPreferencesRequest request, CancellationToken ct = default);
}
