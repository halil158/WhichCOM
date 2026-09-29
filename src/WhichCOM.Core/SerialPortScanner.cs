using WhichCOM.Core.Devices;
using WhichCOM.Core.Settings;

namespace WhichCOM.Core;

/// <summary>Builds the list of serial ports from the raw device entries.</summary>
public sealed class SerialPortScanner
{
    private readonly IPortEntrySource _source;
    private readonly IDeviceNodeReader _nodes;
    private readonly ChipDatabase _chips;

    public SerialPortScanner(IPortEntrySource source, IDeviceNodeReader nodes, ChipDatabase chips)
    {
        _source = source;
        _nodes = nodes;
        _chips = chips;
    }

    /// <summary>Scanner that reads the devices of this computer.</summary>
    public static SerialPortScanner CreateDefault(string settingsDirectory) =>
        new(new WmiPortEntrySource(), new CfgMgrDeviceNodeReader(), ChipDatabase.Load(settingsDirectory));

    /// <summary>All serial ports ordered by port number.</summary>
    public IReadOnlyList<SerialPortInfo> Scan(WhichComSettings settings, bool includeHidden = false)
    {
        var ports = new List<SerialPortInfo>();

        foreach (var entry in _source.GetPortEntries())
        {
            if (!PortName.TryParse(entry.Name, out var portName, out var portNumber, out var description))
            {
                continue;
            }

            var id = PnpDeviceIdParser.Parse(entry.PnpDeviceId);
            var node = _nodes.Read(entry.PnpDeviceId);
            var serial = id.SerialNumber ?? PnpDeviceIdParser.SerialFromParent(id, node.ParentDeviceId);
            var chip = _chips.Lookup(id.Vid, id.Pid);

            var port = new SerialPortInfo
            {
                PortName = portName,
                PortNumber = portNumber,
                Description = description,
                PnpDeviceId = id.Raw,
                Bus = id.Bus,
                Vid = id.Vid,
                Pid = id.Pid,
                SerialNumber = serial,
                LocationPath = node.LocationPath,
                Manufacturer = entry.Manufacturer,
                ChipLabel = chip?.Label,
                ChipTags = chip?.Tags ?? [],
                Alias = AliasResolver.Resolve(settings.Aliases, id.VidPid, serial, node.LocationPath),
                LastArrival = node.LastArrival,
            };

            if (includeHidden || settings.ShowBluetoothPorts || !port.IsBluetooth)
            {
                ports.Add(port);
            }
        }

        ports.Sort((a, b) => a.PortNumber.CompareTo(b.PortNumber));
        return ports;
    }
}
