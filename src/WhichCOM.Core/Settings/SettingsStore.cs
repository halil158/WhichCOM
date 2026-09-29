using System.Text.Json;

namespace WhichCOM.Core.Settings;

/// <summary>Reads and writes settings.json in the WhichCOM settings directory.</summary>
public sealed class SettingsStore
{
    public const string DirectoryOverrideVariable = "WHICHCOM_SETTINGS_DIR";
    public const string FileName = "settings.json";

    public SettingsStore(string? directory = null)
    {
        Directory = directory ?? DefaultDirectory();
    }

    public string Directory { get; }

    public string FilePath => Path.Combine(Directory, FileName);

    /// <summary>Message of the last load failure, or null when the last load succeeded.</summary>
    public string? LastError { get; private set; }

    public static string DefaultDirectory()
    {
        var overridden = Environment.GetEnvironmentVariable(DirectoryOverrideVariable);
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return overridden;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WhichCOM");
    }

    /// <summary>Never throws: a missing or broken file yields default settings.</summary>
    public WhichComSettings Load()
    {
        LastError = null;

        try
        {
            if (!File.Exists(FilePath))
            {
                return new WhichComSettings();
            }

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize(json, WhichComJsonContext.Default.WhichComSettings)
                ?? new WhichComSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LastError = ex.Message;
            return new WhichComSettings();
        }
    }

    public void Save(WhichComSettings settings)
    {
        System.IO.Directory.CreateDirectory(Directory);

        // Write to a temporary file first so a reader never sees a half-written file.
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, WhichComJsonContext.Default.WhichComSettings));
        File.Move(temporary, FilePath, overwrite: true);
    }
}
