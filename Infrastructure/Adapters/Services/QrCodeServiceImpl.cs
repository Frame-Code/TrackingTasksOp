using Application.Ports.Services;
using QRCoder;

namespace Infrastructure.Adapters.Services;

public class QrCodeServiceImpl : IQrCodeService
{
    public string ToPngDataUri(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);

        // PngByteQRCode y no QRCode: este no depende de System.Drawing, que en Linux exige
        // libgdiplus instalado. La app apunta a correr en un container, así que esa dependencia
        // no puede entrar.
        var png = new PngByteQRCode(data).GetGraphic(10);

        return $"data:image/png;base64,{Convert.ToBase64String(png)}";
    }
}
