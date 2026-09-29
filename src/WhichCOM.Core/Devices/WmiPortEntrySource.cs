using System.Management;

namespace WhichCOM.Core.Devices;

/// <summary>Lists the devices of the Ports (COM &amp; LPT) class through WMI.</summary>
public sealed class WmiPortEntrySource : IPortEntrySource
{
    public const string PortsClassGuid = "{4d36e978-e325-11ce-bfc1-08002be10318}";

    private const string Query =
        "SELECT Name, PNPDeviceID, Manufacturer FROM Win32_PnPEntity " +
        "WHERE ClassGuid = '" + PortsClassGuid + "' AND Present = TRUE";

    public IReadOnlyList<PortEntry> GetPortEntries()
    {
        var entries = new List<PortEntry>();

        using var searcher = new ManagementObjectSearcher(Query);
        using var results = searcher.Get();

        foreach (var item in results)
        {
            using (item)
            {
                var name = item["Name"] as string;
                var deviceId = item["PNPDeviceID"] as string;

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(deviceId))
                {
                    entries.Add(new PortEntry(name, deviceId, item["Manufacturer"] as string));
                }
            }
        }

        return entries;
    }
}
