using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace WhichCOM.Core.Cards;

/// <summary>Builds the data of the Serial Ports widget. The layout is in the card templates.</summary>
public static partial class SerialPortsCard
{
    public const string CopyVerb = "copy";
    public const string DeviceManagerVerb = "deviceManager";
    public const string PortProperty = "port";

    private const string Separator = " · ";

    [GeneratedRegex(@"^COM\d{1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex PortNameRegex();

    /// <summary>Number of ports a card of the given size has room for.</summary>
    public static int Capacity(CardSize size) => size switch
    {
        CardSize.Small => 4,
        CardSize.Medium => 3,
        _ => 4,
    };

    public static JsonObject BuildData(
        IReadOnlyList<SerialPortInfo> ports,
        CardSize size,
        CardStrings strings,
        DateTimeOffset now,
        TimeSpan newWindow,
        string? copiedPort = null) =>
        BuildData(ports, Capacity(size), strings, now, newWindow, copiedPort);

    /// <param name="capacity">Number of ports the card has room for.</param>
    public static JsonObject BuildData(
        IReadOnlyList<SerialPortInfo> ports,
        int capacity,
        CardStrings strings,
        DateTimeOffset now,
        TimeSpan newWindow,
        string? copiedPort = null)
    {
        var visible = SelectVisible(ports, capacity);
        var hidden = ports.Count - visible.Count;
        var more = hidden > 0
            ? string.Format(CultureInfo.InvariantCulture, strings.More, hidden)
            : string.Empty;

        var rows = new JsonArray();
        foreach (var port in visible)
        {
            rows.Add(BuildRow(port, strings, PortQuery.IsNew(port, now, newWindow), port.PortName == copiedPort));
        }

        var summary = string.Join(Separator, visible.Select(port => port.PortName));
        if (hidden > 0)
        {
            summary += $" +{hidden}";
        }

        return new JsonObject
        {
            ["hasPorts"] = ports.Count > 0,
            ["isEmpty"] = ports.Count == 0,
            ["summary"] = summary,
            ["subtitle"] = BuildSubtitle(ports, strings),
            ["ports"] = rows,
            ["hasMore"] = hidden > 0,
            ["more"] = more,
            ["strings"] = new JsonObject
            {
                ["title"] = strings.SerialPorts,
                ["noDevice"] = strings.NoDevice,
                ["new"] = strings.New,
                ["deviceManager"] = strings.DeviceManager,
            },
        };
    }

    /// <summary>Port name of a copy action, or null when the action data is not a valid port.</summary>
    public static string? ReadPortFromAction(string? actionData)
    {
        if (string.IsNullOrWhiteSpace(actionData))
        {
            return null;
        }

        try
        {
            var port = JsonNode.Parse(actionData)?[PortProperty]?.GetValue<string>();
            return port is not null && PortNameRegex().IsMatch(port) ? port : null;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    // When there is not enough room, recently plugged USB devices are the ones worth showing.
    private static List<SerialPortInfo> SelectVisible(IReadOnlyList<SerialPortInfo> ports, int capacity)
    {
        if (ports.Count <= capacity)
        {
            return ports.OrderBy(port => port.PortNumber).ToList();
        }

        return ports
            .OrderByDescending(port => port.IsUsb)
            .ThenByDescending(port => port.LastArrival ?? DateTimeOffset.MinValue)
            .ThenByDescending(port => port.PortNumber)
            .Take(capacity)
            .OrderBy(port => port.PortNumber)
            .ToList();
    }

    private static JsonObject BuildRow(SerialPortInfo port, CardStrings strings, bool isNew, bool copied)
    {
        // The label already shows the alias or the chip; do not repeat it in the detail line.
        var parts = new List<string>();
        if (port.Alias is not null && port.ChipLabel is not null)
        {
            parts.Add(port.ChipLabel);
        }
        else if (port.Alias is not null || port.ChipLabel is not null)
        {
            parts.Add(port.Description);
        }

        if (port.VidPid is not null)
        {
            parts.Add(port.VidPid);
        }

        var detail = string.Join(Separator, parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        var serial = port.SerialNumber is null ? string.Empty : $"{strings.SerialNumber} {port.SerialNumber}";

        return new JsonObject
        {
            [PortProperty] = port.PortName,
            ["label"] = port.DisplayLabel,
            ["detail"] = detail,
            ["hasDetail"] = detail.Length > 0,
            ["serial"] = serial,
            ["hasSerial"] = serial.Length > 0,
            ["isNew"] = isNew,
            ["rowStyle"] = isNew ? "emphasis" : "default",
            // A click on the row copies the port name; the confirmation is shown for a moment.
            ["copyTitle"] = strings.Copy,
            ["isCopied"] = copied,
            ["copiedText"] = strings.Copied,
        };
    }

    private static string BuildSubtitle(IReadOnlyList<SerialPortInfo> ports, CardStrings strings)
    {
        if (ports.Count == 0)
        {
            return string.Empty;
        }

        if (ports.Count == 1)
        {
            return ports[0].DisplayLabel;
        }

        var latest = PortQuery.Latest(ports)!;
        return $"{strings.Latest}: {latest.PortName}{Separator}{latest.DisplayLabel}";
    }
}
