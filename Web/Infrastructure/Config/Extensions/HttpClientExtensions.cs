using System.Net.Http.Headers;
using System.Text;

namespace Web.Infrastructure.Config.Extensions;

public static class HttpClientExtensions
{
    public static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        var uri = configuration.GetSection("OpenProjectConfig:Uri").Value
            ?? throw new ArgumentException("OpenProjectConfig:Uri is not set");
        
        var apiKey = configuration.GetSection("OpenProjectConfig:apiKey").Value
            ?? throw new ArgumentException("OpenProjectConfig:apiKey is not set");

        var clientName = configuration.GetSection("OpenProjectConfig:HttpClientName").Value
            ?? throw new ArgumentException("OpenProjectConfig:HttpClientName is not set");

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"apikey:{apiKey}"));
        services.AddHttpClient(clientName, (client) =>
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.BaseAddress = new Uri(uri);
        });

        services.AddHttpClient("GroqClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}