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
        
        Directory.CreateDirectory(settings.KeyRingPath);
        services.AddDataProtection()
            .SetApplicationName(settings.ApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(settings.KeyRingPath));
        return services;
    }
}