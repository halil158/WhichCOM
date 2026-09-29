using System.Text.Json.Nodes;
using WhichCOM.Core.Cards;

namespace WhichCOM.Core.Tests;

public class SerialPortsCardTests
{
    private static readonly DateTimeOffset Noon = TestPorts.Noon;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(120);
    private static readonly CardStrings English = CardStrings.For("en");

    [Fact]
    public void Empty_list_shows_the_empty_state()
    {
        var data = Build([], CardSize.Medium);

        Assert.False(data["hasPorts"]!.GetValue<bool>());
        Assert.True(data["isEmpty"]!.GetValue<bool>());
        Assert.Empty(data["ports"]!.AsArray());
        Assert.Equal("No device connected", data["strings"]!["noDevice"]!.GetValue<string>());
        Assert.Equal(string.Empty, data["summary"]!.GetValue<string>());
    }

    [Fact]
    public void Summary_lists_port_names_in_port_order()
    {
        var data = Build([TestPorts.Esp32(7), TestPorts.Ch340(3)], CardSize.Small);

        Assert.Equal("COM3 · COM7", data["summary"]!.GetValue<string>());
    }

    [Fact]
    public void Subtitle_of_a_single_port_is_its_label()
    {
        var data = Build([TestPorts.Esp32(7, alias: "Sensor board")], CardSize.Small);

        Assert.Equal("Sensor board", data["subtitle"]!.GetValue<string>());
    }

    [Fact]
    public void Subtitle_of_several_ports_names_the_latest()
    {
        var data = Build(
            [TestPorts.Ch340(3, Noon.AddHours(-1)), TestPorts.Esp32(7, Noon.AddMinutes(-5))],
            CardSize.Small);

        Assert.Equal("Latest: COM7 · ESP32 native USB", data["subtitle"]!.GetValue<string>());
    }

    [Fact]
    public void Row_has_label_detail_and_serial()
    {
        var data = Build([TestPorts.Esp32(7, alias: "Sensor board")], CardSize.Large);

        var row = Assert.Single(data["ports"]!.AsArray())!;
        Assert.Equal("COM7", row["port"]!.GetValue<string>());
        Assert.Equal("Sensor board", row["label"]!.GetValue<string>());
        Assert.Equal("ESP32 native USB · 303A:1001", row["detail"]!.GetValue<string>());
        Assert.Equal("S/N AA:BB:CC:DD:EE:FF", row["serial"]!.GetValue<string>());
        Assert.True(row["hasSerial"]!.GetValue<bool>());
        Assert.Equal("Copy", row["copyTitle"]!.GetValue<string>());
    }

    [Fact]
    public void Detail_does_not_repeat_the_label()
    {
        var rows = Build([TestPorts.BuiltIn(), TestPorts.Ch340(3)], CardSize.Large)["ports"]!.AsArray();

        // Built-in port: the label is the description, there is nothing else to show.
        Assert.Equal("Communications Port", rows[0]!["label"]!.GetValue<string>());
        Assert.Equal(string.Empty, rows[0]!["detail"]!.GetValue<string>());
        Assert.False(rows[0]!["hasDetail"]!.GetValue<bool>());
        Assert.False(rows[0]!["hasSerial"]!.GetValue<bool>());

        // Known chip without alias: label is the chip, detail is the description and the IDs.
        Assert.Equal("CH340", rows[1]!["label"]!.GetValue<string>());
        Assert.Equal("USB-SERIAL CH340 · 1A86:7523", rows[1]!["detail"]!.GetValue<string>());
    }

    [Fact]
    public void Recently_plugged_port_is_marked_as_new()
    {
        var rows = Build(
            [TestPorts.Ch340(3, Noon.AddHours(-1)), TestPorts.Esp32(7, Noon.AddSeconds(-30))],
            CardSize.Medium)["ports"]!.AsArray();

        Assert.False(rows[0]!["isNew"]!.GetValue<bool>());
        Assert.Equal("default", rows[0]!["rowStyle"]!.GetValue<string>());
        Assert.True(rows[1]!["isNew"]!.GetValue<bool>());
        Assert.Equal("emphasis", rows[1]!["rowStyle"]!.GetValue<string>());
        Assert.Equal("new", rows[1]!["status"]!.GetValue<string>());
        Assert.Equal("accent", rows[1]!["statusColor"]!.GetValue<string>());
    }

    [Fact]
    public void Copied_port_shows_the_confirmation()
    {
        var rows = SerialPortsCard.BuildData(
            [TestPorts.Ch340(3), TestPorts.Esp32(7)], CardSize.Medium, English, Noon, Window, copiedPort: "COM7")["ports"]!.AsArray();

        Assert.False(rows[0]!["hasStatus"]!.GetValue<bool>());
        Assert.Equal("Copied", rows[1]!["status"]!.GetValue<string>());
        Assert.Equal("good", rows[1]!["statusColor"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(CardSize.Small, 4)]
    [InlineData(CardSize.Medium, 3)]
    [InlineData(CardSize.Large, 4)]
    public void Shows_no_more_ports_than_fit(CardSize size, int capacity)
    {
        var ports = Enumerable.Range(1, 8)
            .Select(number => TestPorts.Ch340(number, Noon.AddMinutes(-number)))
            .ToList();

        var data = Build(ports, size);

        Assert.Equal(capacity, data["ports"]!.AsArray().Count);
        Assert.True(data["hasMore"]!.GetValue<bool>());
        Assert.Equal($"+{8 - capacity} more", data["more"]!.GetValue<string>());
        Assert.EndsWith($" +{8 - capacity}", data["summary"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_recently_plugged_usb_ports_when_there_is_no_room()
    {
        SerialPortInfo[] ports =
        [
            TestPorts.BuiltIn(Noon),
            TestPorts.Ch340(3, Noon.AddDays(-3)),
            TestPorts.Ch340(4, Noon.AddDays(-2)),
            TestPorts.Ch340(5, Noon.AddDays(-1)),
            TestPorts.Esp32(9, Noon.AddMinutes(-1)),
        ];

        var rows = Build(ports, CardSize.Medium)["ports"]!.AsArray();

        Assert.Equal(["COM4", "COM5", "COM9"], rows.Select(row => row!["port"]!.GetValue<string>()));
    }

    [Fact]
    public void Texts_follow_the_language()
    {
        var data = SerialPortsCard.BuildData([TestPorts.Esp32(7, Noon)], CardSize.Large, CardStrings.For("tr"), Noon, Window);

        Assert.Equal("Bağlı cihaz yok", data["strings"]!["noDevice"]!.GetValue<string>());
        Assert.Equal("yeni", data["strings"]!["new"]!.GetValue<string>());
        Assert.Equal("Aygıt Yöneticisi", data["strings"]!["deviceManager"]!.GetValue<string>());
        Assert.Equal("Kopyala", data["ports"]![0]!["copyTitle"]!.GetValue<string>());
        Assert.StartsWith("Seri no ", data["ports"]![0]!["serial"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("en", "en")]
    [InlineData("de", "en")]
    [InlineData("tr", "tr")]
    [InlineData(" TR ", "tr")]
    public void Unknown_languages_fall_back_to_english(string? language, string expected)
    {
        Assert.Equal(expected, CardStrings.For(language).Language);
    }

    [Theory]
    [InlineData("""{"port":"COM7"}""", "COM7")]
    [InlineData("""{"port":"COM256","other":1}""", "COM256")]
    [InlineData("""{"port":"com7"}""", null)]
    [InlineData("""{"port":"COM7; calc.exe"}""", null)]
    [InlineData("""{"port":"COM"}""", null)]
    [InlineData("""{"port":7}""", null)]
    [InlineData("""{"name":"COM7"}""", null)]
    [InlineData("""["COM7"]""", null)]
    [InlineData("not json", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Copy_action_accepts_only_port_names(string? actionData, string? expected)
    {
        Assert.Equal(expected, SerialPortsCard.ReadPortFromAction(actionData));
    }

    private static JsonObject Build(IReadOnlyList<SerialPortInfo> ports, CardSize size) =>
        SerialPortsCard.BuildData(ports, size, English, Noon, Window);
}
