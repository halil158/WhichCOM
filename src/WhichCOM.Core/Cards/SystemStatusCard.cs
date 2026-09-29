using System.Globalization;
using System.Text.Json.Nodes;
using WhichCOM.Core.Status;

namespace WhichCOM.Core.Cards;

/// <summary>Builds the data of the System Status widget. The layout is in the card templates.</summary>
public static class SystemStatusCard
{
    public const string WiFiIcon = "\U0001F4F6";
    public const string EthernetIcon = "\U0001F310";
    public const string NoNetworkIcon = "⚠️";
    public const string OutputIcon = "\U0001F50A";
    public const string NoOutputIcon = "\U0001F507";
    public const string InputIcon = "\U0001F3A4";

    /// <summary>Number of serial ports the large card has room for below the status rows.</summary>
    public const int LargeCardPortCapacity = 3;

    private const string Separator = " · ";
    private const int MaxLinks = 2;

    /// <summary>Data of the large card: the status followed by the serial ports.</summary>
    public static JsonObject BuildData(
        SystemStatus status,
        IReadOnlyList<SerialPortInfo> ports,
        CardStrings strings,
        DateTimeOffset now,
        TimeSpan newWindow,
        string? copiedPort = null)
    {
        var data = BuildData(status, strings);
        data["serial"] = SerialPortsCard.BuildData(ports, LargeCardPortCapacity, strings, now, newWindow, copiedPort);
        return data;
    }

    public static JsonObject BuildData(SystemStatus status, CardStrings strings)
    {
        var links = status.Links
            .OrderByDescending(link => link.IsConnected)
            .ThenBy(link => link.Kind)
            .ThenBy(link => link.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var connected = links.Where(link => link.IsConnected).ToList();

        var linkRows = new JsonArray();
        foreach (var link in links.Take(MaxLinks))
        {
            linkRows.Add(Row(Icon(link), Title(link), Detail(link, strings)));
        }

        if (links.Count == 0)
        {
            linkRows.Add(Row(NoNetworkIcon, strings.NoNetwork, string.Empty));
        }

        var audioRows = new JsonArray
        {
            status.AudioOutput is null
                ? Row(NoOutputIcon, strings.NoAudioOutput, string.Empty)
                : Row(OutputIcon, status.AudioOutput, strings.AudioOutput),
            status.AudioInput is null
                ? Row(InputIcon, strings.NoAudioInput, string.Empty)
                : Row(InputIcon, status.AudioInput, strings.AudioInput),
        };

        var hasNetwork = connected.Count > 0;

        return new JsonObject
        {
            ["networkLine"] = NetworkLine(connected, status.HasInternet, strings),
            ["audioLine"] = status.AudioOutput is null
                ? $"{NoOutputIcon} {strings.NoAudioOutput}"
                : $"{OutputIcon} {status.AudioOutput}",
            ["links"] = linkRows,
            ["audio"] = audioRows,
            ["internet"] = status.HasInternet ? strings.Internet : strings.NoInternet,
            ["internetColor"] = status.HasInternet ? "good" : hasNetwork ? "attention" : "default",
            ["hasInternet"] = status.HasInternet,
        };
    }

    /// <summary>Link speed in the largest unit that keeps the number short, e.g. "2.5 Gbps".</summary>
    public static string FormatSpeed(long bitsPerSecond)
    {
        if (bitsPerSecond <= 0)
        {
            return string.Empty;
        }

        return bitsPerSecond switch
        {
            >= 1_000_000_000 => Number(bitsPerSecond / 1_000_000_000d) + " Gbps",
            >= 1_000_000 => Number(bitsPerSecond / 1_000_000d) + " Mbps",
            _ => Number(bitsPerSecond / 1_000d) + " kbps",
        };

        static string Number(double value) =>
            Math.Round(value, value < 10 ? 1 : 0).ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static JsonObject Row(string icon, string title, string detail) => new()
    {
        ["icon"] = icon,
        ["title"] = title,
        ["detail"] = detail,
        ["hasDetail"] = detail.Length > 0,
    };

    private static string Icon(NetworkLink link) => link.Kind == LinkKind.WiFi ? WiFiIcon : EthernetIcon;

    private static string Title(NetworkLink link)
    {
        if (link.Kind == LinkKind.WiFi && link.IsConnected && !string.IsNullOrWhiteSpace(link.Ssid))
        {
            return link.Ssid;
        }

        return link.Name;
    }

    private static string Detail(NetworkLink link, CardStrings strings)
    {
        if (!link.IsConnected)
        {
            return strings.NotConnected;
        }

        var parts = new List<string>();

        if (link.Kind == LinkKind.WiFi)
        {
            if (link.SignalPercent is { } signal)
            {
                parts.Add(string.Create(CultureInfo.InvariantCulture, $"{Math.Clamp(signal, 0, 100)}%"));
            }
        }
        else if (link.SpeedBitsPerSecond is { } speed && FormatSpeed(speed) is { Length: > 0 } text)
        {
            parts.Add(text);
        }

        if (!string.IsNullOrWhiteSpace(link.IPv4))
        {
            parts.Add(link.IPv4);
        }

        return parts.Count > 0 ? string.Join(Separator, parts) : strings.Connected;
    }

    private static string NetworkLine(IReadOnlyList<NetworkLink> connected, bool hasInternet, CardStrings strings)
    {
        if (connected.Count == 0)
        {
            return $"{NoNetworkIcon} {strings.NoNetwork}";
        }

        var line = string.Join("   ", connected.Take(MaxLinks).Select(link => $"{Icon(link)} {Title(link)}"));
        return hasInternet ? line : $"{line} ({strings.NoInternet})";
    }
}
