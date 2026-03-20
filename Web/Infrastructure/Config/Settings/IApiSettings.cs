namespace Web.Infrastructure.Config.Settings;

public interface IApiSettings
{
    string GetHttpClientName();
    string GetUri();
}