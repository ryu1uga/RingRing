using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace RingRing.Server;

public static class NetworkInfo
{
    /// <summary>true si la IP es loopback o de un rango privado / enlace local.</summary>
    public static bool IsLocalNetwork(IPAddress? address)
    {
        if (address is null) return false;
        if (IPAddress.IsLoopback(address)) return true;

        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal) return true;
            var first = address.GetAddressBytes()[0];
            return (first & 0xFE) == 0xFC; // fc00::/7 (ULA)
        }

        return false;
    }

    /// <summary>IPv4 privadas de las interfaces activas, la de Wi-Fi/Ethernet con gateway primero.</summary>
    public static IReadOnlyList<string> GetLanAddresses()
    {
        var results = new List<(string Ip, bool HasGateway)>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

            var props = nic.GetIPProperties();
            var hasGateway = props.GatewayAddresses.Any(g =>
                g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));

            foreach (var ua in props.UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (!IsLocalNetwork(ua.Address) || IPAddress.IsLoopback(ua.Address)) continue;
                results.Add((ua.Address.ToString(), hasGateway));
            }
        }

        return results
            .OrderByDescending(r => r.HasGateway)
            .Select(r => r.Ip)
            .Distinct()
            .ToList();
    }

    public static string? PrimaryUrl(int port)
    {
        var ip = GetLanAddresses().FirstOrDefault();
        return ip is null ? null : $"http://{ip}:{port}";
    }
}
