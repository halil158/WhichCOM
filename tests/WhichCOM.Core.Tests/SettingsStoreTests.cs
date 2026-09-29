using WhichCOM.Core.Settings;

namespace WhichCOM.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("whichcom-tests-");

    public void Dispose() => _directory.Delete(recursive: true);

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var store = new SettingsStore(_directory.FullName);

        var settings = store.Load();

        Assert.Empty(settings.Aliases);
        Assert.False(settings.ShowBluetoothPorts);
        Assert.Equal(120, settings.NewBadgeSeconds);
        Assert.Null(store.LastError);
    }

    [Fact]
    public void Saved_settings_can_be_loaded_again()
    {
        var store = new SettingsStore(Path.Combine(_directory.FullName, "nested"));
        var settings = new WhichComSettings
        {
            ShowBluetoothPorts = true,
            Aliases = [new AliasEntry { Serial = "TESTSERIAL0001", VidPid = "10C4:EA60", Name = "Sensor board" }],
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.True(loaded.ShowBluetoothPorts);
        var alias = Assert.Single(loaded.Aliases);
        Assert.Equal("TESTSERIAL0001", alias.Serial);
        Assert.Equal("Sensor board", alias.Name);
        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Fact]
    public void Hand_written_file_with_comments_is_accepted()
    {
        var store = new SettingsStore(_directory.FullName);
        File.WriteAllText(store.FilePath, """
            {
              // nicknames
              "Aliases": [
                { "serial": "TESTSERIAL0001", "name": "Sensor board" },
              ],
            }
            """);

        var settings = store.Load();

        Assert.Null(store.LastError);
        Assert.Equal("Sensor board", Assert.Single(settings.Aliases).Name);
    }

    [Fact]
    public void Broken_file_gives_defaults_and_reports_the_error()
    {
        var store = new SettingsStore(_directory.FullName);
        File.WriteAllText(store.FilePath, "{ \"aliases\": [ ");

        var settings = store.Load();

        Assert.Empty(settings.Aliases);
        Assert.NotNull(store.LastError);
    }

    [Fact]
    public void Example_settings_file_in_the_repository_is_valid()
    {
        var example = FindInParents("settings.example.json");
        File.Copy(example, Path.Combine(_directory.FullName, SettingsStore.FileName));
        var store = new SettingsStore(_directory.FullName);

        var settings = store.Load();

        Assert.Null(store.LastError);
        Assert.NotEmpty(settings.Aliases);
    }

    private static string FindInParents(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"{fileName} was not found in any parent directory.");
    }
}
