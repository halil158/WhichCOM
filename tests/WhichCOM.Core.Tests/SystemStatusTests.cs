using System.Text.Json.Nodes;
using AdaptiveCards.Templating;
using WhichCOM.Core.Cards;
using WhichCOM.Core.Status;

namespace WhichCOM.Core.Tests;

// Network names and addresses are made up; the addresses are from the documentation range 192.0.2.0/24.
public class SystemStatusTests
{
    private static readonly CardStrings English = CardStrings.For("en");

    private static readonly NetworkLink WiFi = new()
    {
        Kind = LinkKind.WiFi,
        Name = "Wi-Fi",
        IsConnected = true,
        Ssid = "TestNetwork",
        SignalPercent = 72,
        IPv4 = "192.0.2.10",
        SpeedBitsPerSecond = 866_000_000,
    };

    private static readonly NetworkLink Ethernet = new()
    {
        Kind = LinkKind.Ethernet,
        Name = "Ethernet",
        IsConnected = true,
        SpeedBitsPerSecond = 1_000_000_000,
        IPv4 = "192.0.2.11",
    };

    private static readonly SystemStatus Full = new()
    {
        Links = [WiFi, Ethernet],
        HasInternet = true,
        AudioOutput = "Speakers (Test Audio)",
        AudioInput = "Microphone (Test Audio)",
    };

    [Theory]
    [InlineData("vEthernet (Default Switch)", "Hyper-V Virtual Ethernet Adapter")]
    [InlineData("Ethernet 2", "VirtualBox Host-Only Ethernet Adapter")]
    [InlineData("VMware Network Adapter VMnet8", "VMware Virtual Ethernet Adapter for VMnet8")]
    [InlineData("Local Area Connection", "TAP-Windows Adapter V9")]
    [InlineData("Work", "WireGuard Tunnel")]
    [InlineData("Ethernet 3", "Fortinet SSL VPN Virtual Ethernet Adapter")]
    [InlineData("Loopback Pseudo-Interface 1", "Software Loopback Interface 1")]
    [InlineData("Local Area Connection* 1", "Microsoft Wi-Fi Direct Virtual Adapter")]
    [InlineData("Bluetooth Network Connection", "Bluetooth Device (Personal Area Network)")]
    [InlineData("Tailscale", "Tailscale Tunnel")]
    [InlineData("Ethernet (Kernel Debugger)", "Microsoft Kernel Debug Network Adapter")]
    [InlineData("Local Area Connection* 9", "WAN Miniport (IP)")]
    public void Virtual_adapters_are_recognized(string name, string description)
    {
        Assert.True(NetworkAdapterFilter.IsVirtual(name, description));
        Assert.True(NetworkAdapterFilter.IsVirtual(name, description, hardwareInterface: true));
    }

    [Theory]
    [InlineData("Ethernet", "Realtek PCIe GbE Family Controller")]
    [InlineData("Ethernet 2", "Intel(R) Ethernet Connection I219-V")]
    [InlineData("Wi-Fi", "Intel(R) Wi-Fi 6 AX201 160MHz")]
    [InlineData("Wi-Fi 2", "Qualcomm Atheros Wireless Network Adapter")]
    [InlineData("Ethernet 4", "USB Ethernet Adapter")]
    public void Physical_adapters_are_kept(string name, string description)
    {
        Assert.False(NetworkAdapterFilter.IsVirtual(name, description));
        Assert.False(NetworkAdapterFilter.IsVirtual(name, description, hardwareInterface: true));
    }

    [Fact]
    public void Adapter_without_hardware_is_virtual_whatever_its_name()
    {
        Assert.True(NetworkAdapterFilter.IsVirtual("Ethernet 5", "Some Adapter", hardwareInterface: false));
        Assert.False(NetworkAdapterFilter.IsVirtual(null, null));
    }

    [Theory]
    [InlineData(1_000_000_000, "1 Gbps")]
    [InlineData(2_500_000_000, "2.5 Gbps")]
    [InlineData(10_000_000_000, "10 Gbps")]
    [InlineData(100_000_000, "100 Mbps")]
    [InlineData(866_700_000, "867 Mbps")]
    [InlineData(54_000_000, "54 Mbps")]
    [InlineData(5_500_000, "5.5 Mbps")]
    [InlineData(64_000, "64 kbps")]
    [InlineData(0, "")]
    [InlineData(-1, "")]
    public void Formats_link_speed(long bitsPerSecond, string expected)
    {
        Assert.Equal(expected, SystemStatusCard.FormatSpeed(bitsPerSecond));
    }

    [Fact]
    public void Shows_wifi_and_ethernet_details()
    {
        var links = SystemStatusCard.BuildData(Full, English)["links"]!.AsArray();

        Assert.Equal(2, links.Count);

        Assert.Equal(SystemStatusCard.EthernetIcon, links[0]!["icon"]!.GetValue<string>());
        Assert.Equal("Ethernet", links[0]!["title"]!.GetValue<string>());
        Assert.Equal("1 Gbps · 192.0.2.11", links[0]!["detail"]!.GetValue<string>());

        Assert.Equal(SystemStatusCard.WiFiIcon, links[1]!["icon"]!.GetValue<string>());
        Assert.Equal("TestNetwork", links[1]!["title"]!.GetValue<string>());
        Assert.Equal("72% · 192.0.2.10", links[1]!["detail"]!.GetValue<string>());
    }

    [Fact]
    public void Wifi_without_network_name_shows_the_adapter_name()
    {
        var status = Full with { Links = [WiFi with { Ssid = null, SignalPercent = null }] };

        var data = SystemStatusCard.BuildData(status, English);
        var link = Assert.Single(data["links"]!.AsArray())!;

        Assert.Equal("Wi-Fi", link["title"]!.GetValue<string>());
        Assert.Equal("192.0.2.10", link["detail"]!.GetValue<string>());
        Assert.Equal($"{SystemStatusCard.WiFiIcon} Wi-Fi", data["networkLine"]!.GetValue<string>());
    }

    [Fact]
    public void Connected_adapters_come_first_and_only_two_are_shown()
    {
        var status = Full with
        {
            Links =
            [
                Ethernet with { Name = "Ethernet 2", IsConnected = false, IPv4 = null, SpeedBitsPerSecond = null },
                WiFi,
                Ethernet,
            ],
        };

        var links = SystemStatusCard.BuildData(status, English)["links"]!.AsArray();

        Assert.Equal(["Ethernet", "TestNetwork"], links.Select(link => link!["title"]!.GetValue<string>()));
    }

    [Fact]
    public void Disconnected_adapter_says_so()
    {
        var status = new SystemStatus
        {
            Links = [Ethernet with { IsConnected = false, IPv4 = null, SpeedBitsPerSecond = null }],
        };

        var data = SystemStatusCard.BuildData(status, English);
        var link = Assert.Single(data["links"]!.AsArray())!;

        Assert.Equal("Not connected", link["detail"]!.GetValue<string>());
        Assert.Equal($"{SystemStatusCard.NoNetworkIcon} No network", data["networkLine"]!.GetValue<string>());
        Assert.Equal("No internet", data["internet"]!.GetValue<string>());
        Assert.Equal("default", data["internetColor"]!.GetValue<string>());
    }

    [Fact]
    public void Computer_without_adapters_shows_no_network()
    {
        var data = SystemStatusCard.BuildData(new SystemStatus(), English);

        var link = Assert.Single(data["links"]!.AsArray())!;
        Assert.Equal("No network", link["title"]!.GetValue<string>());
        Assert.False(link["hasDetail"]!.GetValue<bool>());
    }

    [Fact]
    public void Network_without_internet_is_highlighted()
    {
        var data = SystemStatusCard.BuildData(Full with { HasInternet = false }, English);

        Assert.Equal("No internet", data["internet"]!.GetValue<string>());
        Assert.Equal("attention", data["internetColor"]!.GetValue<string>());
        Assert.EndsWith("(No internet)", data["networkLine"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void Internet_access_is_shown_in_the_good_color()
    {
        var data = SystemStatusCard.BuildData(Full, English);

        Assert.Equal("Internet access", data["internet"]!.GetValue<string>());
        Assert.Equal("good", data["internetColor"]!.GetValue<string>());
    }

    [Fact]
    public void Shows_default_audio_devices()
    {
        var data = SystemStatusCard.BuildData(Full, English);
        var audio = data["audio"]!.AsArray();

        Assert.Equal("Speakers (Test Audio)", audio[0]!["title"]!.GetValue<string>());
        Assert.Equal("Output", audio[0]!["detail"]!.GetValue<string>());
        Assert.Equal("Microphone (Test Audio)", audio[1]!["title"]!.GetValue<string>());
        Assert.Equal("Input", audio[1]!["detail"]!.GetValue<string>());
        Assert.Equal($"{SystemStatusCard.OutputIcon} Speakers (Test Audio)", data["audioLine"]!.GetValue<string>());
    }

    [Fact]
    public void Missing_audio_devices_are_named()
    {
        var data = SystemStatusCard.BuildData(Full with { AudioOutput = null, AudioInput = null }, English);
        var audio = data["audio"]!.AsArray();

        Assert.Equal("No output device", audio[0]!["title"]!.GetValue<string>());
        Assert.Equal("No input device", audio[1]!["title"]!.GetValue<string>());
        Assert.Equal($"{SystemStatusCard.NoOutputIcon} No output device", data["audioLine"]!.GetValue<string>());
    }

    [Fact]
    public void Texts_follow_the_language()
    {
        var status = Full with { Links = [Ethernet with { IsConnected = false }], HasInternet = false, AudioInput = null };

        var data = SystemStatusCard.BuildData(status, CardStrings.For("tr"));

        Assert.Equal("Bağlı değil", data["links"]![0]!["detail"]!.GetValue<string>());
        Assert.Equal("İnternet yok", data["internet"]!.GetValue<string>());
        Assert.Equal("Çıkış", data["audio"]![0]!["detail"]!.GetValue<string>());
        Assert.Equal("Giriş cihazı yok", data["audio"]![1]!["title"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("small")]
    [InlineData("medium")]
    public void Card_expands_completely(string size)
    {
        foreach (var status in new[] { Full, new SystemStatus() })
        {
            var card = Expand(size, status);

            Assert.DoesNotContain("${", card.ToJsonString(), StringComparison.Ordinal);
            Assert.DoesNotContain("$when", card.ToJsonString(), StringComparison.Ordinal);
            Assert.NotEmpty(card["body"]!.AsArray());
        }
    }

    [Fact]
    public void Small_card_shows_network_and_audio_output()
    {
        var texts = Texts(Expand("small", Full));

        Assert.Equal(
            [
                $"{SystemStatusCard.EthernetIcon} Ethernet   {SystemStatusCard.WiFiIcon} TestNetwork",
                $"{SystemStatusCard.OutputIcon} Speakers (Test Audio)",
            ],
            texts);
    }

    [Fact]
    public void Medium_card_shows_all_details()
    {
        var texts = Texts(Expand("medium", Full));

        Assert.Equal(
            [
                SystemStatusCard.EthernetIcon, "Ethernet", "1 Gbps · 192.0.2.11",
                SystemStatusCard.WiFiIcon, "TestNetwork", "72% · 192.0.2.10",
                "Internet access",
                SystemStatusCard.OutputIcon, "Speakers (Test Audio)", "Output",
                SystemStatusCard.InputIcon, "Microphone (Test Audio)", "Input",
            ],
            texts);
    }

    [Fact]
    public void Large_card_adds_the_serial_ports()
    {
        var noon = TestPorts.Noon;
        SerialPortInfo[] ports = [TestPorts.Ch340(3, noon.AddHours(-1)), TestPorts.Esp32(7, noon.AddSeconds(-30), alias: "Sensor board")];

        var data = SystemStatusCard.BuildData(Full, ports, English, noon, TimeSpan.FromSeconds(120), copiedPort: "COM3");
        var card = ExpandLarge(data);
        var texts = Texts(card);

        Assert.DoesNotContain("${", card.ToJsonString(), StringComparison.Ordinal);
        Assert.Equal(
            [
                SystemStatusCard.EthernetIcon, "Ethernet", "1 Gbps · 192.0.2.11",
                SystemStatusCard.WiFiIcon, "TestNetwork", "72% · 192.0.2.10",
                "Internet access",
                SystemStatusCard.OutputIcon, "Speakers (Test Audio)", "Output",
                SystemStatusCard.InputIcon, "Microphone (Test Audio)", "Input",
                "Serial ports",
                "CH340",
                "Sensor board",
            ],
            texts);

        var json = card.ToJsonString();
        Assert.Contains("\"title\":\"Copied\",\"verb\":\"copy\",\"data\":{\"port\":\"COM3\"}", json, StringComparison.Ordinal);
        Assert.Contains("\"title\":\"Copy\",\"verb\":\"copy\",\"data\":{\"port\":\"COM7\"}", json, StringComparison.Ordinal);
        Assert.Contains("new", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Large_card_without_ports_says_so()
    {
        var data = SystemStatusCard.BuildData(Full, [], English, TestPorts.Noon, TimeSpan.FromSeconds(120));

        var texts = Texts(ExpandLarge(data));

        Assert.Equal(["Serial ports", "No device connected"], texts.TakeLast(2));
    }

    [Fact]
    public void Large_card_limits_the_serial_ports()
    {
        var ports = Enumerable.Range(1, 7).Select(number => TestPorts.Ch340(number)).ToList();

        var data = SystemStatusCard.BuildData(Full, ports, English, TestPorts.Noon, TimeSpan.FromSeconds(120));

        Assert.Equal(SystemStatusCard.LargeCardPortCapacity, data["serial"]!["ports"]!.AsArray().Count);
        Assert.Equal("+3 more", Texts(ExpandLarge(data))[^1]);
    }

    private static JsonNode ExpandLarge(JsonObject data)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Templates", "SystemStatus.large.json");
        return JsonNode.Parse(new AdaptiveCardTemplate(File.ReadAllText(path)).Expand(data.ToJsonString()))!;
    }

    private static JsonNode Expand(string size, SystemStatus status)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Templates", $"SystemStatus.{size}.json");
        var template = new AdaptiveCardTemplate(File.ReadAllText(path));

        return JsonNode.Parse(template.Expand(SystemStatusCard.BuildData(status, English).ToJsonString()))!;
    }

    private static List<string> Texts(JsonNode? node)
    {
        var texts = new List<string>();
        Collect(node, texts);
        return texts;

        static void Collect(JsonNode? current, List<string> found)
        {
            switch (current)
            {
                case JsonObject item:
                    if (item["type"]?.GetValue<string>() == "TextBlock")
                    {
                        found.Add(item["text"]!.GetValue<string>());
                    }

                    foreach (var property in item)
                    {
                        Collect(property.Value, found);
                    }

                    break;

                case JsonArray array:
                    foreach (var element in array)
                    {
                        Collect(element, found);
                    }

                    break;
            }
        }
    }
}
