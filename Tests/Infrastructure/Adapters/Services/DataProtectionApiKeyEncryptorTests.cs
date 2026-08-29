using Infrastructure.Adapters.Services;
using Infrastructure.Exceptions;
using Microsoft.AspNetCore.DataProtection;

namespace Tests.Infrastructure.Adapters.Services;

/// <summary>
/// Cubre el caso que dejó a los usuarios sin poder operar: un dato cifrado con un key ring que
/// ya no existe. Antes subía como CryptographicException y el usuario recibía un 500 opaco.
/// </summary>
public class DataProtectionApiKeyEncryptorTests
{
    private static DataProtectionApiKeyEncryptorImpl BuildEncryptor(string applicationName) =>
        new(DataProtectionProvider.Create(applicationName));

    [Fact]
    public void UnProtect_ConElMismoKeyRing_DevuelveElValorOriginal()
    {
        var encryptor = BuildEncryptor("TrackingTaskOp");

        var cipher = encryptor.Protect("mi-api-key");

        Assert.Equal("mi-api-key", encryptor.UnProtect(cipher));
    }

    [Fact]
    public void UnProtect_ConOtroKeyRing_LanzaInvalidApiKeyException()
    {
        // Cifrar con un ring y descifrar con otro es exactamente lo que pasa tras restaurar un
        // backup sin su key ring, o tras cambiar el ApplicationName.
        var cipher = BuildEncryptor("TrackingTaskOp").Protect("mi-api-key");
        var otroRing = BuildEncryptor("OtroApplicationName");

        var ex = Assert.Throws<InvalidApiKeyException>(() => otroRing.UnProtect(cipher));

        Assert.Contains("Volvé a cargar tu API key", ex.Message);
    }

    [Fact]
    public void UnProtect_ConTextoQueNoEsCifrado_LanzaInvalidApiKeyException()
    {
        var encryptor = BuildEncryptor("TrackingTaskOp");

        Assert.Throws<InvalidApiKeyException>(() => encryptor.UnProtect("esto-no-es-un-cifrado"));
    }
}
