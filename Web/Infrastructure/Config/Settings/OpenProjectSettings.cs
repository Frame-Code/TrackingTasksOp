namespace Web.Infrastructure.Config.Settings;

public class OpenProjectSettings(IConfiguration configuration) : IApiSettings
{
    public string GetHttpClientName()
    {
        var clientName = configuration.GetSection("OpenProjectConfig:HttpClientName").Value
            ?? throw new ArgumentException("OpenProjectConfig:HttpClientName is not set");
        return clientName;
    }

    public string GetUri()
    {
        var uri = configuration.GetSection("OpenProjectConfig:Uri").Value
            ?? throw new ArgumentException("OpenProjectConfig:Uri is not set");
        return uri;
    }
}

