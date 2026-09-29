using System.Reflection;
using System.Text.Json;

namespace WhichCOM.Core;

public sealed record ChipInfo
{
    public required string Vid { get; init; }

    /// <summary>Four hex digits, or "*" to match every product of the vendor.</summary>
    public required string Pid { get; init; }

    public required string Label { get; init; }

    public string? Vendor { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}

public sealed class ChipTable
{
    public List<ChipInfo> Chips { get; set; } = [];
}

/// <summary>
/// Maps USB VID/PID pairs to a chip label. The built-in table lives in Data/usb-serial-chips.json;
/// users can extend or override it with a chips.json file in the settings directory.
/// </summary>
public sealed class ChipDatabase
{
    public const string WildcardPid = "*";
    public const string UserFileName = "chips.json";

    private const string ResourceName = "WhichCOM.Core.Data.usb-serial-chips.json";

    private readonly Dictionary<string, ChipInfo> _exact = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ChipInfo> _byVendor = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _exact.Count + _byVendor.Count;

    public static ChipDatabase LoadDefault()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream);

        var database = new ChipDatabase();
        database.Add(reader.ReadToEnd());
        return database;
    }

    /// <summary>Built-in table plus the optional user table. A broken user table is ignored.</summary>
    public static ChipDatabase Load(string settingsDirectory)
    {
        var database = LoadDefault();
        var userFile = Path.Combine(settingsDirectory, UserFileName);

        try
        {
            if (File.Exists(userFile))
            {
                database.Add(File.ReadAllText(userFile));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException)
        {
        }

        return database;
    }

    public static ChipDatabase Parse(string json)
    {
        var database = new ChipDatabase();
        database.Add(json);
        return database;
    }

    /// <summary>Adds the entries of a JSON table. Later entries replace earlier ones with the same VID/PID.</summary>
    public void Add(string json)
    {
        var table = JsonSerializer.Deserialize(json, WhichComJsonContext.Default.ChipTable)
            ?? throw new FormatException("Chip table is empty.");

        foreach (var chip in table.Chips)
        {
            Add(chip);
        }
    }

    public void Add(ChipInfo chip)
    {
        var vid = NormalizeHex(chip.Vid, nameof(chip.Vid));
        if (string.IsNullOrWhiteSpace(chip.Label))
        {
            throw new FormatException($"Chip entry {chip.Vid}:{chip.Pid} has no label.");
        }

        if (chip.Pid?.Trim() == WildcardPid)
        {
            _byVendor[vid] = chip with { Vid = vid, Pid = WildcardPid };
            return;
        }

        var pid = NormalizeHex(chip.Pid, nameof(chip.Pid));
        _exact[$"{vid}:{pid}"] = chip with { Vid = vid, Pid = pid };
    }

    /// <summary>Exact VID/PID match first, then the vendor wildcard.</summary>
    public ChipInfo? Lookup(string? vid, string? pid)
    {
        if (string.IsNullOrEmpty(vid))
        {
            return null;
        }

        if (!string.IsNullOrEmpty(pid) && _exact.TryGetValue($"{vid}:{pid}", out var exact))
        {
            return exact;
        }

        return _byVendor.GetValueOrDefault(vid);
    }

    private static string NormalizeHex(string? value, string field)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length != 4 || !text.All(Uri.IsHexDigit))
        {
            throw new FormatException($"Chip entry has an invalid {field} '{value}'. Expected four hex digits.");
        }

        return text.ToUpperInvariant();
    }
}
