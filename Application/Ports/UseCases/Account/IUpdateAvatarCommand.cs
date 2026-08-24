using Application.Dto.Auth;

namespace Application.Ports.UseCases.Account;

public interface IUpdateAvatarCommand
{
    /// <summary>
    /// Guarda el avatar del usuario, o lo borra si JpegBase64 viene null/vacío (mismo criterio
    /// que IUpdateAiApiKeyCommand). Valida que lo recibido sea realmente un JPEG y que no exceda
    /// el tamaño máximo: el navegador ya redimensiona, pero el cliente puede mentir.
    /// </summary>
    Task Execute(UpdateAvatarRequest request, CancellationToken ct = default);
}
