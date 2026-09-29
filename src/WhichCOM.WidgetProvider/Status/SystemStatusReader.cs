using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ManagedNativeWifi;
using NAudio.CoreAudioApi;
using Windows.Networking.Connectivity;
using WhichCOM.Core.Status;

namespace WhichCOM.WidgetProvider.Status;

/// <summary>Reads network and audio status. A part that cannot be read is left empty.</summary>
internal static partial class SystemStatusReader
{
    private static readonly Guid NetworkListManagerClass = new("DCB00C01-570F-4A9B-8D69-199FDBA5723B");

    // Failures repeat at every refresh; each distinct one is logged once.
    private static readonly HashSet<string> Reported = [];

    /// <param name="useWifiApi">
    /// Reads the exact network name and signal quality from the Wi-Fi API. Windows asks the user
    /// for location access when an application does that, so it is off unless the user opts in.
    /// </param>
    public static SystemStatus Read(bool useWifiApi) => new()
    {
        Links = Try("network adapters", () => ReadLinks(useWifiApi), []),
        HasInternet = Try("internet connectivity", ReadInternet, false),
        AudioOutput = Try("audio output", () => ReadDefaultDevice(DataFlow.Render), null),
        AudioInput = Try("audio input", () => ReadDefaultDevice(DataFlow.Capture), null),
    };

    private static IReadOnlyList<NetworkLink> ReadLinks(bool useWifiApi)
    {
        var wifi = useWifiApi ? Try("Wi-Fi", ReadWifi, []) : [];
        var profiles = Try("network profiles", ReadProfiles, []);
        var hardware = Try("interface table", ReadHardwareInterfaces, null);
        var links = new List<NetworkLink>();

        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            LinkKind kind;
            switch (adapter.NetworkInterfaceType)
            {
                case NetworkInterfaceType.Ethernet:
                case NetworkInterfaceType.Ethernet3Megabit:
                case NetworkInterfaceType.FastEthernetT:
                case NetworkInterfaceType.FastEthernetFx:
                case NetworkInterfaceType.GigabitEthernet:
                    kind = LinkKind.Ethernet;
                    break;

                case NetworkInterfaceType.Wireless80211:
                    kind = LinkKind.WiFi;
                    break;

                default:
                    continue;
            }

            // Adapters that were plugged in earlier, such as a tethered phone, stay in the list.
            if (adapter.OperationalStatus == OperationalStatus.NotPresent)
            {
                continue;
            }

            // Windows also lists one entry per filter driver of an adapter; only the entry
            // of the hardware itself is wanted.
            var hasId = Guid.TryParse(adapter.Id, out var id);
            bool? isHardware = hardware is null ? null : hasId && hardware.Contains(id);
            if (NetworkAdapterFilter.IsVirtual(adapter.Name, adapter.Description, isHardware))
            {
                continue;
            }

            var connected = adapter.OperationalStatus == OperationalStatus.Up;
            WifiConnection? connection = null;
            if (kind == LinkKind.WiFi && connected && hasId && !wifi.TryGetValue(id, out connection))
            {
                profiles.TryGetValue(id, out connection);
            }

            links.Add(new NetworkLink
            {
                Kind = kind,
                Name = adapter.Name,
                IsConnected = connected,
                SpeedBitsPerSecond = connected && adapter.Speed > 0 ? adapter.Speed : null,
                IPv4 = connected ? FirstIPv4(adapter.GetIPProperties()) : null,
                Ssid = connection?.Ssid,
                SignalPercent = connection?.Signal,
            });
        }

        return links;
    }

    private static string? FirstIPv4(IPInterfaceProperties properties) =>
        properties.UnicastAddresses
            .Select(address => address.Address)
            .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork)
            ?.ToString();

    // Since Windows 11 24H2 these calls need location access and fail without it.
    // The network profile is used then.
    private static Dictionary<Guid, WifiConnection> ReadWifi()
    {
        var connections = new Dictionary<Guid, WifiConnection>();

        foreach (var adapter in NativeWifi.EnumerateInterfaceConnections())
        {
            if (!adapter.IsConnected)
            {
                continue;
            }

            var (result, connection) = NativeWifi.GetCurrentConnection(adapter.Id);
            if (result == ActionResult.Success && connection is not null)
            {
                connections[adapter.Id] = new WifiConnection(connection.Ssid?.ToString(), connection.SignalQuality);
            }
        }

        return connections;
    }

    // Needs no permission: the name of the network profile, which Windows creates from the
    // network name, and the signal strength in bars.
    private static Dictionary<Guid, WifiConnection> ReadProfiles()
    {
        const int PercentPerBar = 20;
        var connections = new Dictionary<Guid, WifiConnection>();

        foreach (var profile in NetworkInformation.GetConnectionProfiles())
        {
            if (!profile.IsWlanConnectionProfile
                || profile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.None
                || profile.NetworkAdapter is not { } adapter)
            {
                continue;
            }

            int? signal = profile.GetSignalBars() is { } bars ? bars * PercentPerBar : null;
            connections[adapter.NetworkAdapterId] = new WifiConnection(profile.ProfileName, signal);
        }

        return connections;
    }

    private static bool ReadInternet()
    {
        var type = Type.GetTypeFromCLSID(NetworkListManagerClass, throwOnError: true)!;
        dynamic manager = Activator.CreateInstance(type)!;

        try
        {
            return (bool)manager.IsConnectedToInternet;
        }
        finally
        {
            Marshal.ReleaseComObject(manager);
        }
    }

    private static string? ReadDefaultDevice(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        if (!enumerator.TryGetDefaultAudioEndpoint(flow, Role.Multimedia, out var device))
        {
            return null;
        }

        using (device)
        {
            return device.FriendlyName;
        }
    }

    // IDs of the interfaces that are hardware and not a filter driver on top of it.
    // MIB_IF_TABLE2 is a count followed by MIB_IF_ROW2 entries.
    private static unsafe HashSet<Guid> ReadHardwareInterfaces()
    {
        const int FirstRowOffset = 8;
        const int RowSize = 1352;
        const int GuidOffset = 12;
        const int FlagsOffset = 1152;
        const byte HardwareInterface = 0x01;
        const byte FilterInterface = 0x02;

        var result = GetIfTable2(out var table);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception((int)result);
        }

        try
        {
            var interfaces = new HashSet<Guid>();
            var count = *(uint*)table;

            for (var i = 0; i < count; i++)
            {
                var row = table + FirstRowOffset + (i * RowSize);
                var flags = row[FlagsOffset];

                if ((flags & HardwareInterface) != 0 && (flags & FilterInterface) == 0)
                {
                    interfaces.Add(*(Guid*)(row + GuidOffset));
                }
            }

            return interfaces;
        }
        finally
        {
            FreeMibTable(table);
        }
    }

    private static T Try<T>(string what, Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            var key = $"{what}: {ex.GetType().Name}";
            bool first;
            lock (Reported)
            {
                first = Reported.Add(key);
            }

            if (first)
            {
                Log.Error($"Could not read {what}", ex);
            }

            return fallback;
        }
    }

    [LibraryImport("iphlpapi.dll")]
    private static unsafe partial uint GetIfTable2(out byte* table);

    [LibraryImport("iphlpapi.dll")]
    private static unsafe partial void FreeMibTable(byte* table);

    private sealed record WifiConnection(string? Ssid, int? Signal);
}
