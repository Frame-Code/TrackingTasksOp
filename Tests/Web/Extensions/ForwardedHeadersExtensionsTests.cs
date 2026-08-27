using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Web.Extensions;

namespace Tests.WebExtensions;

public class ForwardedHeadersExtensionsTests
{
    private static ForwardedHeadersOptions Build(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

        var services = new ServiceCollection();
        services.AddProxyHeaders(configuration);

        return services.BuildServiceProvider()
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;
    }

    [Fact]
    public void SinProxiesConfigurados_NoConfiaEnNadie()
    {
        var options = Build();

        // El default de ASP.NET es confiar en loopback. Si lo dejáramos, un proceso en la misma
        // máquina (o un contenedor vecino que salga por loopback) podría dictar su propia IP con
        // una cabecera puesta a mano y saltarse el rate limiter del login.
        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownNetworks);
    }

    [Fact]
    public void ConProxyYRedConfigurados_LosRegistra()
    {
        var options = Build(
            ("ForwardedHeadersSettings:KnownProxies:0", "10.0.0.5"),
            ("ForwardedHeadersSettings:KnownNetworks:0", "172.18.0.0/16"));

        Assert.Contains(options.KnownProxies, ip => ip.ToString() == "10.0.0.5");

        var network = Assert.Single(options.KnownNetworks);
        Assert.Equal("172.18.0.0", network.Prefix.ToString());
        Assert.Equal(16, network.PrefixLength);
    }

    [Fact]
    public void DetrasDeCloudflare_UsaLaCabeceraQueElBordeReescribe()
    {
        var options = Build(("ForwardedHeadersSettings:ForwardedForHeaderName", "CF-Connecting-IP"));

        // CF-Connecting-IP la reescribe Cloudflare en cada request, así que no se puede falsificar
        // desde afuera. X-Forwarded-For sí: la manda el cliente.
        Assert.Equal("CF-Connecting-IP", options.ForwardedForHeaderName);
    }

    [Fact]
    public void EntradasInvalidas_SeIgnoranSinRomperElArranque()
    {
        var options = Build(
            ("ForwardedHeadersSettings:KnownProxies:0", "no-es-una-ip"),
            ("ForwardedHeadersSettings:KnownNetworks:0", "172.18.0.0"),
            ("ForwardedHeadersSettings:KnownNetworks:1", "172.18.0.0/xx"));

        // Un typo en el appsettings del deploy no debe tumbar la app al arrancar; lo que hace es
        // dejar de confiar en ese proxy, que es el lado seguro del error.
        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownNetworks);
    }

    [Fact]
    public void HonraIpYEsquemaDelProxy()
    {
        var options = Build();

        // XForwardedProto es lo que evita el bucle de redirecciones cuando el proxy termina el
        // TLS y nos habla en HTTP plano.
        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor));
        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto));
    }
}
