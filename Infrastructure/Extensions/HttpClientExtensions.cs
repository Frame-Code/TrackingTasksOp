using System.Net.Http.Headers;
using Infrastructure.Adapters.Http;
using Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extensions;

public static class HttpClientExtensions
{
    public static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<OpenProjectAuthHandler>();
        services.AddHttpClient(KeyedServicesNames.OpenProjectHttpClientName, client =>
        {
            // Placeholder necesario para que HttpClient acepte rutas relativas.
            // OpenProjectAuthHandler lo reemplaza en runtime con la URL real del usuario.
            client.BaseAddress = new Uri("http://op-placeholder");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        })
        .AddHttpMessageHandler<OpenProjectAuthHandler>();

        var modelName = configuration
            .GetSection("AIModel")
            .GetChildren()
            .First();
        
        AiModelClientFactory.CreateClient(services, configuration, modelName);
        return services;
    }
}