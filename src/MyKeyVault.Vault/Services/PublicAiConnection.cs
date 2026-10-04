using System.Net;
using System.Net.Sockets;

namespace MyKeyVault.Vault.Services;

public static class PublicAiConnection
{
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false, UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(20),
        ConnectCallback = async (context, cancellationToken) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
            if (addresses.Length == 0 || addresses.Any(IsPrivate)) throw new HttpRequestException("AI endpoint must resolve to public addresses.");
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            try { await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken); return new NetworkStream(socket, ownsSocket: true); }
            catch { socket.Dispose(); throw; }
        }
    };
    public static bool IsPrivate(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast) return true;
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6) return (bytes[0] & 0xe0) != 0x20;
        return bytes[0] is 0 or 10 or 127 || bytes[0] >= 224 ||
            (bytes[0] == 169 && bytes[1] == 254) || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
            (bytes[0] == 198 && bytes[1] is 18 or 19);
    }
}
