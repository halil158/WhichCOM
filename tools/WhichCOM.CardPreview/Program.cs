using System.Reflection;
using System.Text.Json.Nodes;
using AdaptiveCards.Templating;
using WhichCOM.Core;
using WhichCOM.Core.Cards;
using WhichCOM.Core.Status;

// Usage: WhichCOM.CardPreview <templates folder> <output file>
// Expands every card template with made-up data and writes one web page that draws the cards.

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: WhichCOM.CardPreview <templates folder> <output file>");
    return 2;
}

var templates = args[0];
var output = args[1];
var now = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
var window = TimeSpan.FromSeconds(120);

SerialPortInfo[] ports =
[
    new()
    {
        PortName = "COM1", PortNumber = 1, Description = "Communications Port",
        PnpDeviceId = @"ACPI\PNP0501\1", Bus = "ACPI",
    },
    new()
    {
        PortName = "COM3", PortNumber = 3, Description = "USB-SERIAL CH340",
        PnpDeviceId = @"USB\VID_1A86&PID_7523\5&1A2B3C4D&0&2", Bus = "USB", Vid = "1A86", Pid = "7523",
        ChipLabel = "CH340", LocationPath = "PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(3)",
        LastArrival = now.AddHours(-3),
    },
    new()
    {
        PortName = "COM7", PortNumber = 7, Description = "USB Serial Device",
        PnpDeviceId = @"USB\VID_303A&PID_1001&MI_00\6&AB12CD34&0&0000", Bus = "USB", Vid = "303A", Pid = "1001",
        ChipLabel = "ESP32 native USB", SerialNumber = "AA:BB:CC:DD:EE:FF", Alias = "Sensor board #2",
        LastArrival = now.AddSeconds(-40),
    },
];

var status = new SystemStatus
{
    Links =
    [
        new NetworkLink
        {
            Kind = LinkKind.Ethernet, Name = "Ethernet", IsConnected = true,
            SpeedBitsPerSecond = 1_000_000_000, IPv4 = "192.0.2.11",
        },
        new NetworkLink
        {
            Kind = LinkKind.WiFi, Name = "Wi-Fi", IsConnected = true,
            Ssid = "TestNetwork", SignalPercent = 80, IPv4 = "192.0.2.10",
        },
    ],
    HasInternet = true,
    AudioOutput = "Speakers (High Definition Audio Device)",
    AudioInput = "Microphone (USB Microphone)",
};

var cards = new JsonArray();

foreach (var language in new[] { CardStrings.English, CardStrings.Turkish })
{
    var strings = CardStrings.For(language);

    Add("System Status", "large", "SystemStatus.large", SystemStatusCard.BuildData(status, ports, strings, now, window, copiedPort: "COM3"));
    Add("System Status", "medium", "SystemStatus.medium", SystemStatusCard.BuildData(status, strings));
    Add("System Status", "small", "SystemStatus.small", SystemStatusCard.BuildData(status, strings));
    Add("Serial Ports", "large", "SerialPorts.large", SerialPortsCard.BuildData(ports, CardSize.Large, strings, now, window));
    Add("Serial Ports", "medium", "SerialPorts.medium", SerialPortsCard.BuildData(ports, CardSize.Medium, strings, now, window));
    Add("Serial Ports", "small", "SerialPorts.small", SerialPortsCard.BuildData(ports, CardSize.Small, strings, now, window));
    Add("Serial Ports, no device", "medium", "SerialPorts.medium", SerialPortsCard.BuildData([], CardSize.Medium, strings, now, window));
    Add("System Status, nothing connected", "large", "SystemStatus.large", SystemStatusCard.BuildData(new SystemStatus(), [], strings, now, window));
    Add("Nicknames", "large", "Nicknames", NicknameCard.BuildData(ports, CardSize.Large, strings));
    Add("Nicknames", "medium", "Nicknames", NicknameCard.BuildData(ports, CardSize.Medium, strings));
    Add("Nicknames", "small", "Nicknames", NicknameCard.BuildData(ports, CardSize.Small, strings));

    void Add(string title, string size, string template, JsonObject data)
    {
        var path = Path.Combine(templates, template + ".json");
        var card = new AdaptiveCardTemplate(File.ReadAllText(path)).Expand(data.ToJsonString());

        cards.Add(new JsonObject
        {
            ["title"] = title,
            ["language"] = language,
            ["size"] = size,
            ["card"] = JsonNode.Parse(card),
        });
    }
}

using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("preview.html")!;
using var reader = new StreamReader(stream);
var page = reader.ReadToEnd().Replace("/*CARDS*/[]", cards.ToJsonString(), StringComparison.Ordinal);

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, page);
Console.WriteLine($"{cards.Count} cards written to {output}");
return 0;
