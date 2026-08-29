namespace Infrastructure.Settings;

public class DataProtectionSettings
{
    public string ApplicationName { get; set; } = null!;

    /// <summary>
    /// Ruta al .pfx que cifra el key ring guardado en la base. Vacío = ring en claro
    /// (aceptable en desarrollo local; en el server el certificado debe estar presente).
    /// </summary>
    public string? KeyRingCertificatePath { get; set; }

    public string? KeyRingCertificatePassword { get; set; }
}
