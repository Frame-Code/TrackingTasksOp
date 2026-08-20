using Application.Dto.Auth;

namespace Application.Ports.UseCases.Settings;

public interface IUpdateAiApiKeyCommand
{
    Task Execute(UpdateAiApiKeyRequest request, CancellationToken ct = default);
}
