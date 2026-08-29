using System.Security.Cryptography.X509Certificates;
using Infrastructure.DataAccess;
using Infrastructure.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extensions;

public static class DataProtectionExtensions
{
    public static IServiceCollection AddTrackingDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("DataProtectionSettings").Get<DataProtectionSettings>()
            ?? throw new Exception("DataProtectionSettings not found");

        // El key ring va en la base, no en disco: es la mitad que descifra las API keys de
        // OpenProject, así que tiene que viajar en el mismo backup que los datos cifrados.
        // Con el volumen keysdata eran dos artefactos separados y un pg_dump sin su ring
        // dejaba a todos los usuarios teniendo que recargar la API key a mano.
        var builder = services.AddDataProtection()
            .SetApplicationName(settings.ApplicationName)
            .PersistKeysToDbContext<TrackingTasksDbContext>();

        // Contrapartida de meter el ring en la base: un dump filtrado traería las claves en
        // claro junto a lo que protegen. El certificado las envuelve, y a diferencia del ring
        // (que rota solo cada 90 días) es un archivo estático: se respalda una vez y listo.
        if (!string.IsNullOrWhiteSpace(settings.KeyRingCertificatePath))
        {
            var certificate = new X509Certificate2(
                settings.KeyRingCertificatePath,
                settings.KeyRingCertificatePassword ?? string.Empty,
                X509KeyStorageFlags.EphemeralKeySet);

            builder.ProtectKeysWithCertificate(certificate);
        }

        return services;
    }
}
