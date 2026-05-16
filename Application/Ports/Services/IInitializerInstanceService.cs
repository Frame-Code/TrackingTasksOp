using Application.Dto.Auth;

namespace Application.Ports.Services;

public interface IInitializerInstanceService
{
    Task InitializeAsync(InitializeInstanceRequest request, CancellationToken ct);
}