using System.Net;
using System.Net.Sockets;

namespace BolagsdataMcp.Web;

/// <summary>
/// Makes sure the website tool can only reach the public internet. An AI agent decides which URL to fetch,
/// so without this check a prompt could make the server read internal addresses such as 127.0.0.1,
/// 192.168.x.x or a cloud metadata endpoint (server-side request forgery).
/// </summary>
/// <remarks>
/// The check runs when the connection is opened, after DNS lookup, so it also covers redirects and
/// host names that resolve to private addresses.
/// </remarks>
public static class PublicNetwork
{
    public static HttpMessageHandler CreateHandler() => new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectCallback = ConnectToPublicAddressAsync,
    };

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        var allowed = addresses.Where(IsPublic).ToArray();
        if (allowed.Length == 0)
        {
            throw new HttpRequestException($"Refusing to connect to {host}: it does not resolve to a public internet address.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            var isPrivateOrReserved =
                b[0] == 0 ||                                  // 0.0.0.0/8
                b[0] == 10 ||                                 // 10.0.0.0/8
                (b[0] == 100 && b[1] >= 64 && b[1] <= 127) || // 100.64.0.0/10 carrier-grade NAT
                (b[0] == 169 && b[1] == 254) ||               // 169.254.0.0/16 link-local, cloud metadata
                (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||  // 172.16.0.0/12
                (b[0] == 192 && b[1] == 168) ||               // 192.168.0.0/16
                b[0] >= 224;                                  // multicast and reserved
            return !isPrivateOrReserved;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(address.Equals(IPAddress.IPv6Any)
                || address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6UniqueLocal
                || address.IsIPv6Multicast);
        }

        return false;
    }
}
