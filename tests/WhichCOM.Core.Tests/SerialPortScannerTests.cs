using WhichCOM.Core.Devices;
using WhichCOM.Core.Settings;

namespace WhichCOM.Core.Tests;

public class SerialPortScannerTests
{
    private const string Cp210xId = @"USB\VID_10C4&PID_EA60\TESTSERIAL0001";
    private const string Ch340Id = @"USB\VID_1A86&PID_7523\5&1A2B3C4D&0&2";
    private const string Esp32InterfaceId = @"USB\VID_303A&PID_1001&MI_00\6&AB12CD34&0&0000";
    private const string Esp32ParentId = @"USB\VID_303A&PID_1001\AA:BB:CC:DD:EE:FF";
    private const string BluetoothId = @"BTHENUM\{00001101-0000-1000-8000-00805F9B34FB}_LOCALMFG&0000\7&AB12CD34&0&000000000000_00000000";
    private const string Ch340Location = "PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(3)";

    private static readonly DateTimeOffset Noon = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Builds_ports_sorted_by_number()
    {
        var scanner = Scanner(
            new PortEntry("Silicon Labs CP210x USB to UART Bridge (COM12)", Cp210xId, "Silicon Labs"),
            new PortEntry("USB-SERIAL CH340 (COM3)", Ch340Id, "wch.cn"));

        var ports = scanner.Scan(new WhichComSettings());

        Assert.Equal(["COM3", "COM12"], ports.Select(port => port.PortName));

        var cp210x = ports[1];
        Assert.Equal("10C4:EA60", cp210x.VidPid);
        Assert.Equal("TESTSERIAL0001", cp210x.SerialNumber);
        Assert.Equal("CP210x", cp210x.ChipLabel);
        Assert.Equal("Silicon Labs CP210x USB to UART Bridge", cp210x.Description);
        Assert.Equal("Silicon Labs", cp210x.Manufacturer);
    }

    [Fact]
    public void Skips_entries_without_a_com_port()
    {
        var scanner = Scanner(
            new PortEntry("ECP Printer Port (LPT1)", @"ACPI\PNP0401\1", null),
            new PortEntry("Communications Port (COM1)", @"ACPI\PNP0501\1", null));

        var port = Assert.Single(scanner.Scan(new WhichComSettings()));

        Assert.Equal("COM1", port.PortName);
        Assert.False(port.IsUsb);
        Assert.Null(port.ChipLabel);
    }

    [Fact]
    public void Composite_interface_takes_serial_from_parent_device()
    {
        var nodes = new FakeNodeReader
        {
            [Esp32InterfaceId] = new DeviceNodeInfo { ParentDeviceId = Esp32ParentId, LastArrival = Noon },
        };
        var scanner = Scanner(nodes, new PortEntry("USB Serial Device (COM7)", Esp32InterfaceId, "Microsoft"));

        var port = Assert.Single(scanner.Scan(new WhichComSettings()));

        Assert.Equal("AA:BB:CC:DD:EE:FF", port.SerialNumber);
        Assert.Equal("ESP32 native USB", port.ChipLabel);
        Assert.Contains("esp32", port.ChipTags);
        Assert.Equal(Noon, port.LastArrival);
    }

    [Fact]
    public void Applies_aliases_by_serial_and_by_location()
    {
        var nodes = new FakeNodeReader
        {
            [Ch340Id] = new DeviceNodeInfo { LocationPath = Ch340Location },
        };
        var settings = new WhichComSettings
        {
            Aliases =
            [
                new AliasEntry { Serial = "TESTSERIAL0001", Name = "Sensor board" },
                new AliasEntry { VidPid = "1A86:7523", Location = Ch340Location, Name = "Bench CH340" },
            ],
        };
        var scanner = Scanner(
            nodes,
            new PortEntry("USB-SERIAL CH340 (COM3)", Ch340Id, null),
            new PortEntry("CP210x (COM12)", Cp210xId, null));

        var ports = scanner.Scan(settings);

        Assert.Equal("Bench CH340", ports[0].Alias);
        Assert.Equal("Bench CH340", ports[0].DisplayLabel);
        Assert.Equal("Sensor board", ports[1].Alias);
    }

    [Fact]
    public void Display_label_falls_back_to_chip_then_description()
    {
        var scanner = Scanner(
            new PortEntry("USB-SERIAL CH340 (COM3)", Ch340Id, null),
            new PortEntry("Communications Port (COM1)", @"ACPI\PNP0501\1", null));

        var ports = scanner.Scan(new WhichComSettings());

        Assert.Equal("Communications Port", ports[0].DisplayLabel);
        Assert.Equal("CH340", ports[1].DisplayLabel);
    }

    [Fact]
    public void Bluetooth_ports_are_hidden_unless_requested()
    {
        var scanner = Scanner(
            new PortEntry("Standard Serial over Bluetooth link (COM4)", BluetoothId, "Microsoft"),
            new PortEntry("USB-SERIAL CH340 (COM3)", Ch340Id, null));

        Assert.Equal(["COM3"], scanner.Scan(new WhichComSettings()).Select(port => port.PortName));

        Assert.Equal(
            ["COM3", "COM4"],
            scanner.Scan(new WhichComSettings { ShowBluetoothPorts = true }).Select(port => port.PortName));

        Assert.Equal(
            ["COM3", "COM4"],
            scanner.Scan(new WhichComSettings(), includeHidden: true).Select(port => port.PortName));
    }

    private static SerialPortScanner Scanner(params PortEntry[] entries) => Scanner(new FakeNodeReader(), entries);

    private static SerialPortScanner Scanner(FakeNodeReader nodes, params PortEntry[] entries) =>
        new(new FakeSource(entries), nodes, ChipDatabase.LoadDefault());

    private sealed class FakeSource(IReadOnlyList<PortEntry> entries) : IPortEntrySource
    {
        public IReadOnlyList<PortEntry> GetPortEntries() => entries;
    }

    private sealed class FakeNodeReader : Dictionary<string, DeviceNodeInfo>, IDeviceNodeReader
    {
        public DeviceNodeInfo Read(string pnpDeviceId) =>
            TryGetValue(pnpDeviceId, out var info) ? info : DeviceNodeInfo.Empty;
    }
}
