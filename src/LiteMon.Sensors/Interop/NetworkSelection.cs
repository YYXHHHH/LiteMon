using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LiteMon.Sensors.Interop;

/// <summary>
/// 选择"主网卡"：优先有 IPv4 默认网关的物理网卡，排除 VPN/虚拟网卡。
/// 面板显示与流量统计共用同一选择，避免"名字是 VPN、流量是全部"的错位。
/// </summary>
internal static class NetworkSelection
{
    public static NetworkInterface? Primary()
        => NetworkInterface.GetAllNetworkInterfaces()
            .Where(IsPhysicalUp)
            .OrderByDescending(HasGateway)
            .ThenByDescending(nic => nic.Speed)
            .FirstOrDefault();

    private static bool IsPhysicalUp(NetworkInterface nic)
        => nic.OperationalStatus == OperationalStatus.Up
           && nic.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211
           && !IsVirtual(nic);

    private static bool IsVirtual(NetworkInterface nic)
    {
        string description = nic.Description;
        return description.Contains("VPN", StringComparison.OrdinalIgnoreCase)
               || description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
               || description.Contains("TAP", StringComparison.OrdinalIgnoreCase)
               || description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
               || description.Contains("Radmin", StringComparison.OrdinalIgnoreCase)
               || description.Contains("Loopback", StringComparison.OrdinalIgnoreCase)
               || description.Contains("Tunnel", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasGateway(NetworkInterface nic)
    {
        try
        {
            return nic.GetIPProperties().GatewayAddresses
                .Any(gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork);
        }
        catch (NetworkInformationException)
        {
            return false;
        }
    }
}
