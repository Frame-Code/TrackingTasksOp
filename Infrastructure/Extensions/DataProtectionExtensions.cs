using Infrastructure.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extensions;

public static class DataProtectionExtensions
{
    public static IServiceCollection AddTrackingDataProtection(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        var settings = configuration.GetSection("DataProtectionSettings").Get<DataProtectionSettings>()
            ?? throw new Exception("DataProtectionSettings not found");

        // Carpeta "Keys" dentro del proyecto: estable sin importar SO, terminal o
        // variables de entorno (a diferencia de LocalApplicationData, que en Linux
        // depende de XDG_DATA_HOME y puede variar entre terminales).
        var keyRingPath = Path.Combine(contentRootPath, "Keys");

        Directory.CreateDirectory(keyRingPath);
        services.AddDataProtection()
            .SetApplicationName(settings.ApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
        return services;
    }
}