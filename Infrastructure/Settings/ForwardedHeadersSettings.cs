namespace Infrastructure.Settings;

/// <summary>
/// Proxy inverso delante de la app (Cloudflare Tunnel, nginx). Sin esto, detrás de un proxy
/// TODOS los requests llegan con la IP del proxy: el rate limiter por IP deja de distinguir
/// clientes y pasa a ser un cubo único compartido por todo internet — cualquiera puede
/// quemarlo y dejar sin login al resto.
/// </summary>
public class ForwardedHeadersSettings
{
    /// <summary>IPs exactas de los proxies en los que confiamos. Vacío = no hay proxy (desarrollo).</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>
    /// Redes CIDR ("172.18.0.0/16") para cuando el proxy no tiene IP fija — el caso típico de
    /// un contenedor, que cambia de IP en cada recreación del bridge de Docker.
    /// </summary>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>
    /// Cabecera que trae la IP real del cliente. Detrás de Cloudflare tiene que ser
    /// "CF-Connecting-IP": Cloudflare la reescribe siempre en su borde, así que no se puede
    /// falsificar desde afuera. X-Forwarded-For sí se puede — el cliente la manda a mano —
    /// y por eso solo sirve cuando el proxy la sanea.
    /// </summary>
    public string ForwardedForHeaderName { get; set; } = "X-Forwarded-For";
}
