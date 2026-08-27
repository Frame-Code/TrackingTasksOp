using System.Net;
using Infrastructure.Settings;
using Microsoft.AspNetCore.HttpOverrides;

namespace Web.Extensions;

public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddProxyHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("ForwardedHeadersSettings").Get<ForwardedHeadersSettings>()
            ?? new ForwardedHeadersSettings();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // XForwardedProto además de XForwardedFor: el proxy termina el TLS y nos habla en
            // HTTP plano, así que sin esta cabecera UseHttpsRedirection ve "http", redirige a
            // "https", el proxy vuelve a entrar en HTTP y se arma un bucle de redirecciones.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardedForHeaderName = settings.ForwardedForHeaderName;

            // ASP.NET confía por defecto en loopback. La app corre en contenedor, donde el proxy
            // nunca es loopback, así que esos defaults no sirven y hay que declarar el proxy real.
            // Limpiar primero deja una sola regla: se confía en lo configurado, y en nada más.
            //
            // Que la lista quede vacía es el default SEGURO: sin proxies conocidos el middleware
            // ignora las cabeceras y RemoteIpAddress sigue siendo la conexión real. Lo peligroso
            // es lo contrario — abrirla de más (0.0.0.0/0) deja que cualquiera mande la cabecera
            // a mano y se haga pasar por la IP que quiera, justo lo que el rate limiter necesita
            // que no pase.
            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();

            foreach (var proxy in settings.KnownProxies)
            {
                if (IPAddress.TryParse(proxy, out var address))
                    options.KnownProxies.Add(address);
            }

            foreach (var network in settings.KnownNetworks)
            {
                var parts = network.Split('/');
                if (parts.Length == 2
                    && IPAddress.TryParse(parts[0], out var prefix)
                    && int.TryParse(parts[1], out var prefixLength))
                {
                    // Calificado: .NET 8 agregó System.Net.IPNetwork y el nombre queda ambiguo.
                    options.KnownNetworks.Add(
                        new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength));
                }
            }
        });

        return services;
    }
}
