using System.Net;
using System.Net.Sockets;
using Infrastructure.Exceptions;

namespace Infrastructure.Adapters.Http;

/// <summary>
/// Evita SSRF hacia la red interna del servidor. La URL de la instancia de OpenProject la
/// declara el propio usuario al registrarse, y a partir de ahí se usa para siempre en cada
/// request de ese tenant (ver OpenProjectAuthHandler) — sin este filtro, alguien podría
/// registrarse con "su instancia" apuntando a `localhost`, la red privada del VPS, o el
/// endpoint de metadata de la nube (169.254.169.254), y el servidor terminaría haciendo esas
/// conexiones por él.
///
/// La resolución de DNS se repite en CADA conexión nueva (no se cachea el resultado de una
/// validación anterior): así, un dominio que apunte a una IP pública al momento de
/// registrarse y cambie después a una IP interna (DNS rebinding) sigue quedando bloqueado en
/// cada request posterior, no solo en el primero.
/// </summary>
public static class SsrfSafeHttpHandler
{
    public static SocketsHttpHandler Create() => new()
    {
        ConnectCallback = ConnectAsync
    };

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
        if (addresses.Length == 0)
            throw new HttpRequestException($"No se pudo resolver el host '{context.DnsEndPoint.Host}'.");

        var blocked = Array.Find(addresses, IsDisallowed);
        if (blocked is not null)
            throw new SsrfBlockedException(
                $"La URL de la instancia de OpenProject no puede apuntar a una red privada o interna " +
                $"('{context.DnsEndPoint.Host}' resuelve a {blocked}). Usá la dirección pública de tu instancia.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses[0], context.DnsEndPoint.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Loopback, redes privadas RFC1918, y link-local (incluye la IP de metadata de nube).</summary>
    internal static bool IsDisallowed(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10                              // 10.0.0.0/8
                || (b[0] == 172 && b[1] is >= 16 and <= 31) // 172.16.0.0/12
                || (b[0] == 192 && b[1] == 168)             // 192.168.0.0/16
                || (b[0] == 169 && b[1] == 254);            // 169.254.0.0/16 (link-local + metadata de nube)
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) return true;
            var b = ip.GetAddressBytes();
            return (b[0] & 0xfe) == 0xfc; // fc00::/7 (unique local)
        }

        return false;
    }
}
