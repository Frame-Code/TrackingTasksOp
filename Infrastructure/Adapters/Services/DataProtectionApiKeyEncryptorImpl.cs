using System.Security.Cryptography;
using Application.Ports.Auth;
using Infrastructure.Exceptions;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Adapters.Services;

public class DataProtectionApiKeyEncryptorImpl(IDataProtectionProvider provider): IApiKeyEncryptorService
{
    private readonly IDataProtector _protector = provider.CreateProtector("TrackingTasksOp.OpenProjectApiKey.v1");

    public string Protect(string plain) => _protector.Protect(plain);

    /// <summary>
    /// Traduce el fallo de descifrado a un error accionable.
    ///
    /// Ocurre cuando el key ring que cifró el dato ya no está: un restore que se llevó la base
    /// sin su ring, un <c>ApplicationName</c> cambiado, o un .pfx distinto al que lo envolvió.
    /// El dato guardado es irrecuperable — no hay forma de descifrarlo — así que lo único
    /// accionable es que el usuario vuelva a cargar la credencial.
    ///
    /// El guard va acá y no en cada llamador porque los cuatro descifrados
    /// (<see cref="Infrastructure.Adapters.Http.OpenProjectAuthHeaderProvider"/> descifra la API
    /// key, el access token y el refresh token) pasan por este método. Sin esto, la
    /// <see cref="CryptographicException"/> sube sin capturar y el usuario ve un 500 opaco en vez
    /// de saber qué tiene que hacer.
    /// </summary>
    public string UnProtect(string cipher)
    {
        try
        {
            return _protector.Unprotect(cipher);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidApiKeyException(
                "No se pudo descifrar tu credencial de OpenProject porque la clave de cifrado ya " +
                "no está disponible. Volvé a cargar tu API key en Configuración.", ex);
        }
    }
}
