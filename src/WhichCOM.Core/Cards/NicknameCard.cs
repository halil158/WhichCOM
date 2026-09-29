using System.Text.Json;
using System.Text.Json.Nodes;
using WhichCOM.Core.Settings;

namespace WhichCOM.Core.Cards;

/// <summary>
/// Builds the data of the customization card, where the user gives nicknames to the connected
/// devices, and reads the result back.
/// </summary>
public static class NicknameCard
{
    public const string SaveVerb = "saveNicknames";
    public const string CancelVerb = "exitCustomization";
    public const int MaxNicknameLength = 40;

    private const string InputPrefix = "nickname_";

    /// <summary>Number of devices the card of the given size has room for.</summary>
    public static int Capacity(CardSize size) => size switch
    {
        CardSize.Small => 1,
        CardSize.Medium => 2,
        _ => 5,
    };

    public static JsonObject BuildData(IReadOnlyList<SerialPortInfo> ports, CardSize size, CardStrings strings)
    {
        // Only devices that can be recognized again can have a nickname.
        var candidates = ports
            .Where(port => AliasResolver.CreateEntry(port, string.Empty) is not null)
            .OrderByDescending(port => port.LastArrival ?? DateTimeOffset.MinValue)
            .ThenByDescending(port => port.PortNumber)
            .Take(Capacity(size))
            .OrderBy(port => port.PortNumber)
            .ToList();

        var rows = new JsonArray();
        foreach (var port in candidates)
        {
            rows.Add(new JsonObject
            {
                ["port"] = port.PortName,
                ["inputId"] = InputPrefix + port.PortName,
                ["label"] = $"{port.PortName} · {port.ChipLabel ?? port.Description}",
                ["nickname"] = port.Alias ?? string.Empty,
            });
        }

        return new JsonObject
        {
            ["title"] = strings.Nicknames,
            ["hint"] = strings.NicknameHint,
            ["placeholder"] = strings.NicknamePlaceholder,
            ["devices"] = rows,
            ["hasDevices"] = rows.Count > 0,
            ["isEmpty"] = rows.Count == 0,
            ["noDevices"] = strings.NoDeviceForNickname,
            ["save"] = strings.Save,
            ["cancel"] = strings.Cancel,
            ["maxLength"] = MaxNicknameLength,
        };
    }

    /// <summary>
    /// Nicknames the user entered, by port name. An empty nickname removes the existing one.
    /// Returns an empty list when the action data is not valid.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ReadNicknames(string? actionData)
    {
        var nicknames = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(actionData))
        {
            return nicknames;
        }

        try
        {
            if (JsonNode.Parse(actionData) is not JsonObject values)
            {
                return nicknames;
            }

            foreach (var (key, value) in values)
            {
                if (!key.StartsWith(InputPrefix, StringComparison.Ordinal)
                    || value is not JsonValue text
                    || !text.TryGetValue<string>(out var nickname))
                {
                    continue;
                }

                var port = SerialPortsCard.ReadPortFromAction(
                    new JsonObject { [SerialPortsCard.PortProperty] = key[InputPrefix.Length..] }.ToJsonString());
                if (port is not null)
                {
                    nicknames[port] = Clean(nickname);
                }
            }
        }
        catch (JsonException)
        {
            nicknames.Clear();
        }

        return nicknames;
    }

    /// <summary>Applies the nicknames to the settings. Returns the number of devices changed.</summary>
    public static int Apply(
        WhichComSettings settings,
        IReadOnlyList<SerialPortInfo> ports,
        IReadOnlyDictionary<string, string> nicknames)
    {
        var changed = 0;

        foreach (var port in ports)
        {
            if (!nicknames.TryGetValue(port.PortName, out var nickname)
                || string.Equals(nickname, port.Alias ?? string.Empty, StringComparison.Ordinal))
            {
                continue;
            }

            if (AliasResolver.SetAlias(settings, port, nickname))
            {
                changed++;
            }
        }

        return changed;
    }

    private static string Clean(string nickname)
    {
        var text = new string(nickname.Where(character => !char.IsControl(character)).ToArray()).Trim();
        return text.Length > MaxNicknameLength ? text[..MaxNicknameLength].TrimEnd() : text;
    }
}
