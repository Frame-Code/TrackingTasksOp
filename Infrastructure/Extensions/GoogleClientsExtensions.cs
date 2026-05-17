using Google.Cloud.AIPlatform.V1;
using Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extensions;

/// <summary>
/// Métodos de extensión para registrar los clientes de Google Cloud.
/// </summary>
public static class GoogleClientsExtensions
{
    public static IServiceCollection AddGoogleClients(this IServiceCollection services, IConfiguration configuration)
    {
        // Configura el cliente de Vertex AI Prediction
        services.AddSingleton(provider =>
        {
            var geminiSettings = configuration.GetSection("GeminiSettings").Get<GeminiSettings>();

            if (geminiSettings == null)
            {
                throw new InvalidOperationException("GeminiSettings no está configurado en appsettings.json");
            }
            
            var clientBuilder = new PredictionServiceClientBuilder
            {
                Endpoint = $"{geminiSettings.Location}-aiplatform.googleapis.com"
                // Para autenticación en GCP, la librería usará las credenciales del ambiente
                // (ej. gcloud auth application-default login) o variables de entorno.
            };
            return clientBuilder.Build();
        });

        return services;
    }
}
