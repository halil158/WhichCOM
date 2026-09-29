namespace WhichCOM.Core.Status;

/// <summary>Tells physical network adapters from virtual ones (Hyper-V, VPN, VirtualBox, ...).</summary>
public static class NetworkAdapterFilter
{
    private static readonly string[] VirtualMarkers =
    [
        "virtual",
        "hyper-v",
        "vethernet",
        "vmware",
        "virtualbox",
        "host-only",
        "loopback",
        "vpn",
        "tap-",
        "tap adapter",
        "tunnel",
        "wintun",
        "wireguard",
        "openvpn",
        "tailscale",
        "zerotier",
        "anyconnect",
        "fortinet",
        "miniport",
        "kernel debug",
        "teredo",
        "isatap",
        "6to4",
        "npcap",
        "docker",
        "wsl",
        "bluetooth",
        "wi-fi direct",
    ];

    /// <param name="name">Connection name, e.g. "vEthernet (Default Switch)".</param>
    /// <param name="description">Adapter description, e.g. "Hyper-V Virtual Ethernet Adapter".</param>
    /// <param name="hardwareInterface">
    /// Whether Windows reports a hardware interface; null when that is unknown.
    /// </param>
    public static bool IsVirtual(string? name, string? description, bool? hardwareInterface = null)
    {
        if (hardwareInterface == false)
        {
            return true;
        }

        return ContainsMarker(name) || ContainsMarker(description);
    }

    private static bool ContainsMarker(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var marker in VirtualMarkers)
        {
            if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
