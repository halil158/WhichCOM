using WhichCOM.Core.Settings;

namespace WhichCOM.Core.Tests;

public class AliasResolverTests
{
    private const string Location = "PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(3)";
    private const string OtherLocation = "PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(4)";

    [Fact]
    public void Matches_by_serial_number()
    {
        AliasEntry[] aliases = [new() { Serial = "TESTSERIAL0001", Name = "Sensor board" }];

        Assert.Equal("Sensor board", AliasResolver.Resolve(aliases, "10C4:EA60", "testserial0001", null));
        Assert.Null(AliasResolver.Resolve(aliases, "10C4:EA60", "TESTSERIAL0002", null));
        Assert.Null(AliasResolver.Resolve(aliases, "10C4:EA60", null, null));
    }

    [Fact]
    public void Serial_entry_with_vid_pid_requires_both()
    {
        // Cheap adapters often share a serial number such as "0001".
        AliasEntry[] aliases = [new() { Serial = "0001", VidPid = "10C4:EA60", Name = "Gateway" }];

        Assert.Equal("Gateway", AliasResolver.Resolve(aliases, "10C4:EA60", "0001", null));
        Assert.Null(AliasResolver.Resolve(aliases, "1A86:7523", "0001", null));
    }

    [Fact]
    public void Matches_by_vid_pid_and_location_when_there_is_no_serial()
    {
        AliasEntry[] aliases = [new() { VidPid = "1a86:7523", Location = Location, Name = "Bench CH340" }];

        Assert.Equal("Bench CH340", AliasResolver.Resolve(aliases, "1A86:7523", null, Location));
        Assert.Null(AliasResolver.Resolve(aliases, "1A86:7523", null, OtherLocation));
        Assert.Null(AliasResolver.Resolve(aliases, "10C4:EA60", null, Location));
    }

    [Fact]
    public void Serial_match_wins_over_location_and_vendor_matches()
    {
        AliasEntry[] aliases =
        [
            new() { VidPid = "10C4:EA60", Name = "Any CP210x" },
            new() { VidPid = "10C4:EA60", Location = Location, Name = "Left socket" },
            new() { Serial = "TESTSERIAL0001", Name = "Sensor board" },
        ];

        Assert.Equal("Sensor board", AliasResolver.Resolve(aliases, "10C4:EA60", "TESTSERIAL0001", Location));
        Assert.Equal("Left socket", AliasResolver.Resolve(aliases, "10C4:EA60", "OTHER", Location));
        Assert.Equal("Any CP210x", AliasResolver.Resolve(aliases, "10C4:EA60", "OTHER", OtherLocation));
    }

    [Fact]
    public void Entries_without_a_name_are_ignored()
    {
        AliasEntry[] aliases = [new() { Serial = "TESTSERIAL0001", Name = "  " }];

        Assert.Null(AliasResolver.Resolve(aliases, "10C4:EA60", "TESTSERIAL0001", null));
    }

    [Fact]
    public void Set_alias_adds_replaces_and_removes()
    {
        var settings = new WhichComSettings();
        var port = Port(serial: "TESTSERIAL0001");

        Assert.True(AliasResolver.SetAlias(settings, port, "First"));
        Assert.True(AliasResolver.SetAlias(settings, port, " Second "));

        var entry = Assert.Single(settings.Aliases);
        Assert.Equal("Second", entry.Name);
        Assert.Equal("TESTSERIAL0001", entry.Serial);

        Assert.True(AliasResolver.SetAlias(settings, port, ""));
        Assert.Empty(settings.Aliases);
    }

    [Fact]
    public void Set_alias_uses_location_when_there_is_no_serial()
    {
        var settings = new WhichComSettings();

        Assert.True(AliasResolver.SetAlias(settings, Port(serial: null, location: Location), "Bench"));

        var entry = Assert.Single(settings.Aliases);
        Assert.Null(entry.Serial);
        Assert.Equal("1A86:7523", entry.VidPid);
        Assert.Equal(Location, entry.Location);
    }

    [Fact]
    public void Set_alias_fails_for_a_device_that_cannot_be_identified()
    {
        var settings = new WhichComSettings();

        Assert.False(AliasResolver.SetAlias(settings, Port(serial: null, location: null), "Nope"));
        Assert.Empty(settings.Aliases);
    }

    private static SerialPortInfo Port(string? serial, string? location = null) => new()
    {
        PortName = "COM3",
        PortNumber = 3,
        Description = "USB-SERIAL CH340",
        PnpDeviceId = @"USB\VID_1A86&PID_7523\X",
        Bus = "USB",
        Vid = "1A86",
        Pid = "7523",
        SerialNumber = serial,
        LocationPath = location,
    };
}
