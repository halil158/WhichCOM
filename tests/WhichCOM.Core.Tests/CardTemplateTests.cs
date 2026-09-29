using System.Text.Json.Nodes;
using AdaptiveCards.Templating;
using WhichCOM.Core.Cards;

namespace WhichCOM.Core.Tests;

/// <summary>Expands the card templates with real card data, the way the widget host does.</summary>
public class CardTemplateTests
{
    private static readonly DateTimeOffset Noon = TestPorts.Noon;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(120);

    private static readonly SerialPortInfo[] Ports =
    [
        TestPorts.Ch340(3, Noon.AddHours(-1)),
        TestPorts.Esp32(7, Noon.AddSeconds(-30), alias: "Sensor board"),
    ];

    public static TheoryData<string> TemplateNames()
    {
        var names = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(TemplateDirectory(), "*.json"))
        {
            names.Add(Path.GetFileNameWithoutExtension(file));
        }

        return names;
    }

    [Theory]
    [MemberData(nameof(TemplateNames))]
    public void Template_is_a_valid_adaptive_card(string name)
    {
        var card = JsonNode.Parse(ReadTemplate(name))!;

        Assert.Equal("AdaptiveCard", card["type"]!.GetValue<string>());
        Assert.Equal("1.5", card["version"]!.GetValue<string>());
        Assert.NotEmpty(card["body"]!.AsArray());
    }

    [Theory]
    [MemberData(nameof(TemplateNames))]
    public void Template_uses_no_fixed_colors_or_images(string name)
    {
        var template = ReadTemplate(name);

        // Cards must follow the light and dark theme of the host.
        Assert.DoesNotContain("backgroundImage", template, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch("#[0-9a-fA-F]{6}", template);
    }

    [Theory]
    [InlineData(CardSize.Small)]
    [InlineData(CardSize.Medium)]
    [InlineData(CardSize.Large)]
    public void Serial_ports_card_expands_completely(CardSize size)
    {
        foreach (var ports in new IReadOnlyList<SerialPortInfo>[] { Ports, [] })
        {
            var card = ExpandSerialPorts(size, ports);

            Assert.DoesNotContain("${", card.ToJsonString(), StringComparison.Ordinal);
            Assert.DoesNotContain("$when", card.ToJsonString(), StringComparison.Ordinal);
            Assert.NotEmpty(card["body"]!.AsArray());
        }
    }

    [Fact]
    public void Small_card_shows_the_port_names()
    {
        var texts = Texts(ExpandSerialPorts(CardSize.Small, Ports));

        Assert.Equal(["COM3 · COM7", "Latest: COM7 · Sensor board"], texts);
    }

    [Fact]
    public void Empty_small_card_shows_only_the_empty_text()
    {
        Assert.Equal(["No device connected"], Texts(ExpandSerialPorts(CardSize.Small, [])));
    }

    [Theory]
    [InlineData(CardSize.Medium)]
    [InlineData(CardSize.Large)]
    public void Empty_card_offers_the_device_manager(CardSize size)
    {
        var card = ExpandSerialPorts(size, []);

        Assert.Equal(["No device connected", "Device Manager", "Show all devices", "›"], Texts(card));
        Assert.Single(Actions(card, SerialPortsCard.DeviceManagerVerb));
        Assert.Empty(Actions(card, SerialPortsCard.CopyVerb));
    }

    [Theory]
    [InlineData(CardSize.Medium)]
    [InlineData(CardSize.Large)]
    public void A_click_on_a_row_copies_its_port(CardSize size)
    {
        var card = ExpandSerialPorts(size, Ports);

        var copyActions = Actions(card, SerialPortsCard.CopyVerb);

        Assert.Equal(["COM3", "COM7"], copyActions.Select(action => action["data"]!["port"]!.GetValue<string>()));
        Assert.All(copyActions, action => Assert.Equal("Copy", action["tooltip"]!.GetValue<string>()));

        // The rows stay plain: no buttons and no copy label.
        Assert.DoesNotContain(Descendants(card), node => node["type"]?.GetValue<string>() == "ActionSet");
        Assert.DoesNotContain("Copy", Texts(card));
        Assert.DoesNotContain("Copied", Texts(card));

        // The provider must be able to read back what the card sends.
        Assert.All(copyActions, action =>
            Assert.NotNull(SerialPortsCard.ReadPortFromAction(action["data"]!.ToJsonString())));
    }

    [Theory]
    [InlineData(CardSize.Medium)]
    [InlineData(CardSize.Large)]
    public void Only_the_new_port_is_marked_as_new(CardSize size)
    {
        var texts = Texts(ExpandSerialPorts(size, Ports));

        Assert.Single(texts, text => text == "new");
        Assert.True(texts.IndexOf("new") > texts.IndexOf("COM7"));
    }

    [Fact]
    public void Medium_card_shows_port_and_label_in_every_row()
    {
        var texts = Texts(ExpandSerialPorts(CardSize.Medium, Ports));

        Assert.Equal(["COM3", "CH340", "COM7", "Sensor board", "new"], texts);
    }

    [Theory]
    [InlineData(CardSize.Medium)]
    [InlineData(CardSize.Large)]
    public void Only_the_copied_row_shows_the_confirmation(CardSize size)
    {
        var data = SerialPortsCard.BuildData(Ports, size, CardStrings.For("en"), Noon, Window, copiedPort: "COM7");
        var template = new AdaptiveCardTemplate(ReadTemplate($"SerialPorts.{size.ToString().ToLowerInvariant()}"));

        var texts = Texts(JsonNode.Parse(template.Expand(data.ToJsonString()))!);

        Assert.Single(texts, text => text == "Copied");
        Assert.Equal("Copied", texts[texts.IndexOf("Sensor board") + (size == CardSize.Large ? 3 : 1)]);
    }

    [Fact]
    public void Large_card_shows_details_and_the_device_manager_link()
    {
        var card = ExpandSerialPorts(CardSize.Large, Ports);
        var texts = Texts(card);

        Assert.Contains("USB-SERIAL CH340 · 1A86:7523", texts);
        Assert.Contains("ESP32 native USB · 303A:1001", texts);
        Assert.Contains("S/N AA:BB:CC:DD:EE:FF", texts);
        Assert.Equal(["Device Manager", "Show all devices", "›"], texts.TakeLast(3));

        Assert.Single(Actions(card, SerialPortsCard.DeviceManagerVerb));
        Assert.Null(card["actions"]);

        var styles = card["body"]!.AsArray()
            .Where(node => node!["style"] is not null)
            .Select(node => node!["style"]!.GetValue<string>());
        Assert.Equal(["default", "emphasis"], styles);
    }

    [Fact]
    public void Medium_card_mentions_ports_that_do_not_fit()
    {
        var ports = Enumerable.Range(1, 5).Select(number => TestPorts.Ch340(number)).ToList();

        var texts = Texts(ExpandSerialPorts(CardSize.Medium, ports));

        Assert.Contains("+2 more", texts);
    }

    private static JsonNode ExpandSerialPorts(CardSize size, IReadOnlyList<SerialPortInfo> ports)
    {
        var data = SerialPortsCard.BuildData(ports, size, CardStrings.For("en"), Noon, Window);
        var template = new AdaptiveCardTemplate(ReadTemplate($"SerialPorts.{size.ToString().ToLowerInvariant()}"));

        return JsonNode.Parse(template.Expand(data.ToJsonString()))!;
    }

    private static List<JsonObject> Actions(JsonNode card, string verb) =>
        Descendants(card)
            .Where(node => node["type"]?.GetValue<string>() == "Action.Execute"
                && node["verb"]?.GetValue<string>() == verb)
            .ToList();

    private static List<string> Texts(JsonNode card) =>
        Descendants(card)
            .Where(node => node["type"]?.GetValue<string>() == "TextBlock")
            .Select(node => node["text"]!.GetValue<string>())
            .ToList();

    private static IEnumerable<JsonObject> Descendants(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject current:
                yield return current;
                foreach (var property in current)
                {
                    foreach (var child in Descendants(property.Value))
                    {
                        yield return child;
                    }
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    foreach (var child in Descendants(item))
                    {
                        yield return child;
                    }
                }

                break;
        }
    }

    private static string ReadTemplate(string name) =>
        File.ReadAllText(Path.Combine(TemplateDirectory(), name + ".json"));

    private static string TemplateDirectory() => Path.Combine(AppContext.BaseDirectory, "Templates");
}
