using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extensions;

public static class HttpClientExtensions
{
    public static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        var uri = configuration.GetSection("OpenProjectSettings:BaseUrl").Value
            ?? throw new ArgumentException("OpenProjectSettings:BaseUrl is not set");
        
        var apiKey = configuration.GetSection("OpenProjectSettings:ApiKey").Value
            ?? throw new ArgumentException("OpenProjectSettings:ApiKey is not set");

        var opClientName = configuration.GetSection("OpenProjectSettings:HttpClientName").Value
            ?? throw new ArgumentException("OpenProjectSettings:HttpClientName is not set");

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"apikey:{apiKey}"));
        services.AddHttpClient(opClientName, (client) =>
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.BaseAddress = new Uri(uri);
        });

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