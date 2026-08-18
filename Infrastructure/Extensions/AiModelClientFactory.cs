using System.Net.Http.Headers;
using Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extensions;

//Factory para configurar el cliente de cada modelo de AI
public static class AiModelClientFactory
{
    public static void CreateClient(IServiceCollection services, IConfiguration configuration, IConfigurationSection section)
    {
        var modelName = section.Key;
        var apiKey = section["ApiKey"];
        switch (modelName)
        {
            case "Groq":
                if(string.IsNullOrEmpty(apiKey)) throw new ArgumentException($"{modelName}:ApiKey is not set");
                services.AddHttpClient(KeyedServicesNames.GroqHttpClientName, client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", apiKey);
                });
                break;
            default:
                throw new Exception("No se reconoce el modelo de AI asegúrese de agregalo al factory");
        }
    }
}