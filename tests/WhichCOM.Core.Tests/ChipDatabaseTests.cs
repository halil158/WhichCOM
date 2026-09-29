namespace WhichCOM.Core.Tests;

public class ChipDatabaseTests
{
    private static readonly ChipDatabase Default = ChipDatabase.LoadDefault();

    [Theory]
    [InlineData("10C4", "EA60", "CP210x")]
    [InlineData("1A86", "7523", "CH340")]
    [InlineData("1A86", "55D4", "CH9102")]
    [InlineData("0403", "6001", "FTDI")]
    [InlineData("0403", "6010", "FTDI")]
    [InlineData("0403", "6014", "FTDI")]
    [InlineData("0403", "6015", "FTDI")]
    [InlineData("303A", "1001", "ESP32")]
    public void Built_in_table_knows_common_chips(string vid, string pid, string expectedLabelPart)
    {
        var chip = Default.Lookup(vid, pid);

        Assert.NotNull(chip);
        Assert.Contains(expectedLabelPart, chip.Label, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0043")]
    [InlineData("8036")]
    [InlineData("FFFF")]
    public void Every_arduino_product_matches_by_vendor(string pid)
    {
        Assert.Equal("Arduino", Default.Lookup("2341", pid)?.Label);
    }

    [Fact]
    public void Exact_match_wins_over_vendor_wildcard()
    {
        Assert.Equal("ESP32 native USB", Default.Lookup("303A", "1001")?.Label);
        Assert.Equal("Espressif", Default.Lookup("303A", "0002")?.Label);
    }

    [Fact]
    public void Lookup_is_case_insensitive()
    {
        Assert.Equal("CH9102", Default.Lookup("1a86", "55d4")?.Label);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("FFFF", "FFFF")]
    [InlineData("10C4", null)]
    public void Unknown_ids_give_null(string? vid, string? pid)
    {
        Assert.Null(Default.Lookup(vid, pid));
    }

    [Fact]
    public void User_table_overrides_and_extends_built_in_table()
    {
        var database = ChipDatabase.LoadDefault();

        database.Add("""
            {
              // comments and trailing commas are allowed
              "chips": [
                { "vid": "10c4", "pid": "ea60", "label": "My CP2102" },
                { "vid": "FFF0", "pid": "0001", "label": "Custom board", "tags": [ "custom" ] },
              ]
            }
            """);

        Assert.Equal("My CP2102", database.Lookup("10C4", "EA60")?.Label);
        Assert.Equal("Custom board", database.Lookup("FFF0", "0001")?.Label);
        Assert.Equal(["custom"], database.Lookup("FFF0", "0001")?.Tags);
        Assert.Equal("CH340", database.Lookup("1A86", "7523")?.Label);
    }

    [Theory]
    [InlineData("""{ "chips": [ { "vid": "10C", "pid": "EA60", "label": "x" } ] }""")]
    [InlineData("""{ "chips": [ { "vid": "10C4", "pid": "EA6G", "label": "x" } ] }""")]
    [InlineData("""{ "chips": [ { "vid": "10C4", "pid": "EA60", "label": " " } ] }""")]
    public void Invalid_entries_are_rejected(string json)
    {
        Assert.Throws<FormatException>(() => ChipDatabase.Parse(json));
    }

    [Fact]
    public void Broken_user_file_is_ignored()
    {
        var directory = Directory.CreateTempSubdirectory("whichcom-tests-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, ChipDatabase.UserFileName), "{ not json");

            var database = ChipDatabase.Load(directory.FullName);

            Assert.Equal("CP210x", database.Lookup("10C4", "EA60")?.Label);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
