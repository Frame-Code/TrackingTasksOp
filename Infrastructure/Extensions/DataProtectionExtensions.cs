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

        // Carpeta de datos de aplicación del usuario actual: independiente del SO
        // (Windows: %LOCALAPPDATA%, Linux: ~/.local/share, macOS: ~/Library/Application Support)
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var keyRingPath = Path.Combine(basePath, settings.ApplicationName, "Keys");

        Directory.CreateDirectory(keyRingPath);
        services.AddDataProtection()
            .SetApplicationName(settings.ApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
        return services;
    }
}