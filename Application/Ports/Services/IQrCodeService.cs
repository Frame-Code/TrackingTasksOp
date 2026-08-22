namespace Application.Ports.Services;

public interface IQrCodeService
{
    /// <summary>
    /// Convierte un texto (acá, la URI otpauth://) en un PNG embebido como data URI, listo para
    /// un &lt;img src&gt;. Es un puerto para que la librería de QR quede en Infrastructure y no
    /// se filtre a Application.
    /// </summary>
    string ToPngDataUri(string content);
}
