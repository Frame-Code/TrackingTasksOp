namespace Application.Ports.Services;

public abstract class BaseUrlService
{
    public abstract bool Validate(string url, bool applyCustomValidations = false);
    public string NormalizeUrl(string url)
    {
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri);
        
        var builder = new UriBuilder(uri!)
        {
            Path = "",
            Query = "",
            Fragment = ""
        };

        string resultado = builder.Uri.GetLeftPart(UriPartial.Authority);
        return resultado.TrimEnd('/');
    }
}