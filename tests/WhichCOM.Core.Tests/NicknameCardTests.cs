using System.Text.Json.Nodes;
using AdaptiveCards.Templating;
using WhichCOM.Core.Cards;
using WhichCOM.Core.Settings;

namespace WhichCOM.Core.Tests;

public class NicknameCardTests
{
    private const string Location = "PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(3)";

    private static readonly CardStrings English = CardStrings.For("en");
    private static readonly DateTimeOffset Noon = TestPorts.Noon;

    [Fact]
    public void Lists_devices_with_their_current_nickname()
    {
        SerialPortInfo[] ports =
        [
            TestPorts.Ch340(3) with { LocationPath = Location },
            TestPorts.Esp32(7, alias: "Sensor board"),
        ];

        var devices = NicknameCard.BuildData(ports, CardSize.Large, English)["devices"]!.AsArray();

        Assert.Equal(2, devices.Count);
        Assert.Equal("nickname_COM3", devices[0]!["inputId"]!.GetValue<string>());
        Assert.Equal("COM3 · CH340", devices[0]!["label"]!.GetValue<string>());
        Assert.Equal(string.Empty, devices[0]!["nickname"]!.GetValue<string>());
        Assert.Equal("COM7 · ESP32 native USB", devices[1]!["label"]!.GetValue<string>());
        Assert.Equal("Sensor board", devices[1]!["nickname"]!.GetValue<string>());
    }

    [Fact]
    public void Devices_that_cannot_be_recognized_again_are_left_out()
    {
        // No serial number and no location: a built-in port and an adapter without location.
        SerialPortInfo[] ports = [TestPorts.BuiltIn(), TestPorts.Ch340(3)];

        var data = NicknameCard.BuildData(ports, CardSize.Large, English);

        Assert.Empty(data["devices"]!.AsArray());
        Assert.True(data["isEmpty"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData(CardSize.Small, "COM9")]
    [InlineData(CardSize.Medium, "COM8,COM9")]
    [InlineData(CardSize.Large, "COM5,COM6,COM7,COM8,COM9")]
    public void Shows_the_most_recently_plugged_devices_that_fit(CardSize size, string expected)
    {
        var ports = Enumerable.Range(3, 7)
            .Select(number => TestPorts.Esp32(number, Noon.AddMinutes(number)))
            .ToList();

        var devices = NicknameCard.BuildData(ports, size, English)["devices"]!.AsArray();

        Assert.Equal(expected, string.Join(',', devices.Select(device => device!["port"]!.GetValue<string>())));
    }

    [Fact]
    public void Reads_the_nicknames_the_card_sends()
    {
        var nicknames = NicknameCard.ReadNicknames(
            """{"nickname_COM3":"  Bench adapter ","nickname_COM7":"","other":"x","nickname_LPT1":"no","nickname_COM9":5}""");

        Assert.Equal(2, nicknames.Count);
        Assert.Equal("Bench adapter", nicknames["COM3"]);
        Assert.Equal(string.Empty, nicknames["COM7"]);
    }

    [Fact]
    public void Nicknames_are_cleaned_and_shortened()
    {
        var text = "Line one\nLine two\t" + new string('x', 60);

        var nickname = NicknameCard.ReadNicknames(new JsonObject { ["nickname_COM3"] = text }.ToJsonString())["COM3"];

        Assert.Equal(NicknameCard.MaxNicknameLength, nickname.Length);
        Assert.DoesNotContain('\n', nickname);
        Assert.DoesNotContain('\t', nickname);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void Invalid_action_data_gives_no_nicknames(string? actionData)
    {
        Assert.Empty(NicknameCard.ReadNicknames(actionData));
    }

    [Fact]
    public void Apply_adds_changes_and_removes_nicknames()
    {
        var settings = new WhichComSettings
        {
            Aliases = [new AliasEntry { Serial = "AA:BB:CC:DD:EE:FF", VidPid = "303A:1001", Name = "Old name" }],
        };
        SerialPortInfo[] ports =
        [
            TestPorts.Ch340(3) with { LocationPath = Location },
            TestPorts.Esp32(7, alias: "Old name"),
        ];

        var changed = NicknameCard.Apply(
            settings,
            ports,
            new Dictionary<string, string> { ["COM3"] = "Bench adapter", ["COM7"] = "", ["COM99"] = "Nobody" });

        Assert.Equal(2, changed);
        var alias = Assert.Single(settings.Aliases);
        Assert.Equal("Bench adapter", alias.Name);
        Assert.Equal("1A86:7523", alias.VidPid);
        Assert.Equal(Location, alias.Location);
    }

    [Fact]
    public void Apply_ignores_unchanged_nicknames()
    {
        var settings = new WhichComSettings();

        var changed = NicknameCard.Apply(
            settings,
            [TestPorts.Esp32(7, alias: "Sensor board")],
            new Dictionary<string, string> { ["COM7"] = "Sensor board" });

        Assert.Equal(0, changed);
        Assert.Empty(settings.Aliases);
    }

    [Fact]
    public void Card_has_an_input_for_every_device_and_both_buttons()
    {
        SerialPortInfo[] ports = [TestPorts.Esp32(7, alias: "Sensor board"), TestPorts.Esp32(9)];

        var card = Expand(NicknameCard.BuildData(ports, CardSize.Large, English));

        var inputs = card["body"]!.AsArray().Where(node => node!["type"]!.GetValue<string>() == "Input.Text").ToList();
        Assert.Equal(["nickname_COM7", "nickname_COM9"], inputs.Select(input => input!["id"]!.GetValue<string>()));
        Assert.Equal("Sensor board", inputs[0]!["value"]!.GetValue<string>());
        Assert.Equal("Nickname", inputs[0]!["placeholder"]!.GetValue<string>());

        Assert.Equal(
            [NicknameCard.SaveVerb, NicknameCard.CancelVerb],
            card["actions"]!.AsArray().Select(action => action!["verb"]!.GetValue<string>()));
        Assert.DoesNotContain("${", card.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Card_without_devices_offers_only_cancel()
    {
        var card = Expand(NicknameCard.BuildData([], CardSize.Medium, English));

        Assert.DoesNotContain(card["body"]!.AsArray(), node => node!["type"]!.GetValue<string>() == "Input.Text");
        Assert.Contains("Connect a device", card.ToJsonString(), StringComparison.Ordinal);
        Assert.Equal(
            [NicknameCard.CancelVerb],
            card["actions"]!.AsArray().Select(action => action!["verb"]!.GetValue<string>()));
    }

    private static JsonNode Expand(JsonObject data)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Templates", "Nicknames.json");
        return JsonNode.Parse(new AdaptiveCardTemplate(File.ReadAllText(path)).Expand(data.ToJsonString()))!;
    }
}
