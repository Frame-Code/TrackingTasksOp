namespace Web.Infrastructure.Config.Extensions;

public static class CorsExtensions
{
    public static IServiceCollection ConfigureCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("CorsOrigins").Get<string[]>()
            ?? throw new Exception("CorsOrigins are not configured");
        
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if(origins.Contains("*")) 
                    policy.SetIsOriginAllowed(_ => true)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                else
                    policy.WithOrigins(origins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
            });
        });
        
        return services;
    }
}