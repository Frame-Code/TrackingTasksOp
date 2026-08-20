using System.Net;
using Infrastructure.Adapters.Http;

namespace Tests.Infrastructure.Adapters.Http;

/// <summary>
/// Cubre la lista de rangos bloqueados (loopback, redes privadas, link-local/metadata de
/// nube) que evita el SSRF hacia la red interna del servidor. La resolución de DNS y la
/// conexión real (ConnectAsync) no se prueban acá porque requieren red — lo que importa
/// verificar es la clasificación de IPs, que es donde vive la lógica de seguridad.
/// </summary>
public class SsrfSafeHttpHandlerTests
{
    [Theory]
    [InlineData("127.0.0.1")]        // loopback
    [InlineData("10.0.0.5")]         // 10.0.0.0/8
    [InlineData("172.16.0.1")]       // 172.16.0.0/12 (límite inferior)
    [InlineData("172.31.255.255")]   // 172.16.0.0/12 (límite superior)
    [InlineData("192.168.1.1")]      // 192.168.0.0/16
    [InlineData("169.254.169.254")]  // link-local / metadata de nube (AWS, DigitalOcean, etc.)
    [InlineData("::1")]              // loopback IPv6
    [InlineData("fc00::1")]          // unique local IPv6
    [InlineData("fe80::1")]          // link-local IPv6
    public void IsDisallowed_DireccionPrivadaOInterna_DevuelveTrue(string ip)
    {
        Assert.True(SsrfSafeHttpHandler.IsDisallowed(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("8.8.8.8")]          // pública, fuera de cualquier rango privado
    [InlineData("172.15.255.255")]   // justo debajo del rango 172.16.0.0/12
    [InlineData("172.32.0.0")]       // justo encima del rango 172.16.0.0/12
    [InlineData("192.169.0.1")]      // parecida a 192.168.x pero fuera del rango
    [InlineData("1.1.1.1")]
    public void IsDisallowed_DireccionPublica_DevuelveFalse(string ip)
    {
        Assert.False(SsrfSafeHttpHandler.IsDisallowed(IPAddress.Parse(ip)));
    }
}
