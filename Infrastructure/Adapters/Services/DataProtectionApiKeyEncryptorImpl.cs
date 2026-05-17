using Application.Ports.Auth;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Adapters.Services;

public class DataProtectionApiKeyEncryptorImpl(IDataProtectionProvider provider): IApiKeyEncryptorService
{
    private readonly IDataProtector _protector = provider.CreateProtector("TrackingTasksOp.OpenProjectApiKey.v1");

    public string Protect(string plain) => _protector.Protect(plain);
    public string UnProtect(string cipher) => _protector.Unprotect(cipher);
}