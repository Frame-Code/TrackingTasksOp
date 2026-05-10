using Application.Ports.Services;

namespace Infrastructure.Adapters.Services;

public class OpenProjectUrlServiceImpl : BaseUrlService
{
    public override bool Validate(string url, bool applyCustomValidations = false)
    {
        var basicValidator = Uri.TryCreate(url, UriKind.Absolute, out Uri? uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        
        return !applyCustomValidations? 
            basicValidator
            :basicValidator && (url.Trim().Contains("open", StringComparison.InvariantCultureIgnoreCase) || url.Trim().Contains("project", StringComparison.InvariantCultureIgnoreCase) || url.Trim().Contains("localhost", StringComparison.InvariantCultureIgnoreCase));
    }
}