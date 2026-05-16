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
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        })
        .AddHttpMessageHandler<OpenProjectAuthHandler>();

        var modelClient = configuration.GetSection("Groq:HttpClientName").Value;
        if(modelClient is null)
            return services;
        
        var apiKeyModel = configuration.GetSection("Groq:ApiKey").Value
            ?? throw new ArgumentException("Groq:ApiKey is not set");
        services.AddHttpClient(modelClient, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKeyModel);
        });

        return services;
    }
}