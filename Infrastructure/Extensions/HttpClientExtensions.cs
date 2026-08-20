using System.Net.Http.Headers;
using Infrastructure.Adapters.Http;
using Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extensions;

public static class HttpClientExtensions
{
    public static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        services.AddTransient<OpenProjectAuthHandler>();
        var opClientBuilder = services.AddHttpClient(KeyedServicesNames.OpenProjectHttpClientName, client =>
        {
            // Placeholder necesario para que HttpClient acepte rutas relativas.
            // OpenProjectAuthHandler lo reemplaza en runtime con la URL real del usuario.
            client.BaseAddress = new Uri("http://op-placeholder");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        var validationClientBuilder = services.AddHttpClient(KeyedServicesNames.OpenProjectValidationHttpClientName);

        // El filtro SSRF bloquea IPs privadas/loopback, lo que rompe el flujo normal en
        // desarrollo (la instancia de OpenProject local suele estar en localhost/red privada).
        // Se activa solo fuera de Development, donde la URL de instancia SÍ es un dato externo
        // no confiable declarado por el usuario.
        if (!isDevelopment)
        {
            // El handler protector va como PRIMARIO (el que abre la conexión TCP real);
            // OpenProjectAuthHandler corre antes en la cadena, solo reescribe URL/auth.
            opClientBuilder.ConfigurePrimaryHttpMessageHandler(SsrfSafeHttpHandler.Create);

            // Cliente aparte para validar la API key al registrarse/actualizarla: en ese momento
            // todavía no hay un CurrentUser autenticado (OpenProjectAuthHandler lo necesita), así
            // que no puede reusar el cliente de arriba — pero sí necesita la misma protección SSRF,
            // porque la URL que se valida ahí es la que el usuario recién está declarando.
            validationClientBuilder.ConfigurePrimaryHttpMessageHandler(SsrfSafeHttpHandler.Create);
        }

        opClientBuilder.AddHttpMessageHandler<OpenProjectAuthHandler>();

        var modelName = configuration
            .GetSection("AIModel")
            .GetChildren()
            .First();
        
        AiModelClientFactory.CreateClient(services, configuration, modelName);
        return services;
    }
}