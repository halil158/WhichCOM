namespace WhichCOM.Core.Tests;

// All device IDs and serial numbers in these tests are made up.
public class PnpDeviceIdParserTests
{
    [Fact]
    public void Parses_usb_device_with_serial_number()
    {
        var id = PnpDeviceIdParser.Parse(@"USB\VID_10C4&PID_EA60\TESTSERIAL0001");

        Assert.Equal("USB", id.Bus);
        Assert.Equal("10C4", id.Vid);
        Assert.Equal("EA60", id.Pid);
        Assert.Equal("10C4:EA60", id.VidPid);
        Assert.Equal("TESTSERIAL0001", id.SerialNumber);
        Assert.Null(id.InterfaceNumber);
        Assert.True(id.IsUsb);
    }

    [Fact]
    public void Generated_instance_id_is_not_a_serial_number()
    {
        var id = PnpDeviceIdParser.Parse(@"USB\VID_1A86&PID_7523\5&1A2B3C4D&0&2");

        Assert.Equal("1A86", id.Vid);
        Assert.Equal("7523", id.Pid);
        Assert.Equal("5&1A2B3C4D&0&2", id.InstanceId);
        Assert.Null(id.SerialNumber);
    }

    [Fact]
    public void Composite_interface_has_interface_number_and_no_serial()
    {
        var id = PnpDeviceIdParser.Parse(@"USB\VID_303A&PID_1001&MI_00\6&AB12CD34&0&0000");

        Assert.Equal("303A", id.Vid);
        Assert.Equal("1001", id.Pid);
        Assert.Equal(0, id.InterfaceNumber);
        Assert.Null(id.SerialNumber);
    }

    [Fact]
    public void Interface_number_is_hexadecimal()
    {
        var id = PnpDeviceIdParser.Parse(@"USB\VID_0403&PID_6010&MI_0A\7&AB12CD34&0&000A");

        Assert.Equal(10, id.InterfaceNumber);
    }

    [Fact]
    public void Revision_in_device_segment_is_ignored()
    {
        var id = PnpDeviceIdParser.Parse(@"USB\VID_2341&PID_0043&REV_0001\TEST5550001");

        Assert.Equal("2341:0043", id.VidPid);
        Assert.Equal("TEST5550001", id.SerialNumber);
    }

    [Fact]
    public void Hex_digits_are_normalized_to_upper_case()
    {
        var id = PnpDeviceIdParser.Parse(@"usb\vid_1a86&pid_55d4\TEST0002");

        Assert.Equal("USB", id.Bus);
        Assert.Equal("1A86", id.Vid);
        Assert.Equal("55D4", id.Pid);
    }

    [Fact]
    public void Serial_in_mac_address_form_is_kept()
    {
        var id = PnpDeviceIdParser.Parse(@"USB\VID_303A&PID_1001\AA:BB:CC:DD:EE:FF");

        Assert.Equal("AA:BB:CC:DD:EE:FF", id.SerialNumber);
    }

    [Fact]
    public void Parses_ftdi_bus_id_and_strips_channel_letter()
    {
        var id = PnpDeviceIdParser.Parse(@"FTDIBUS\VID_0403+PID_6001+TESTFT01A\0000");

        Assert.Equal("FTDIBUS", id.Bus);
        Assert.Equal("0403", id.Vid);
        Assert.Equal("6001", id.Pid);
        Assert.Equal("TESTFT01", id.SerialNumber);
        Assert.Equal('A', id.Channel);
        Assert.True(id.IsUsb);
    }

    [Fact]
    public void Ftdi_second_channel_shares_the_serial_number()
    {
        var id = PnpDeviceIdParser.Parse(@"FTDIBUS\VID_0403+PID_6010+TESTFT02B\0000");

        Assert.Equal("TESTFT02", id.SerialNumber);
        Assert.Equal('B', id.Channel);
    }

    [Fact]
    public void Ftdi_device_without_serial_number()
    {
        var id = PnpDeviceIdParser.Parse(@"FTDIBUS\VID_0403+PID_6001+5&1A2B3C4D&0&1\0000");

        Assert.Equal("0403:6001", id.VidPid);
        Assert.Null(id.SerialNumber);
        Assert.Null(id.Channel);
    }

    [Fact]
    public void Bluetooth_port_has_no_usb_ids()
    {
        var id = PnpDeviceIdParser.Parse(
            @"BTHENUM\{00001101-0000-1000-8000-00805F9B34FB}_VID&0001FFFF_PID&0001\7&AB12CD34&0&000000000000_00000000");

        Assert.Equal("BTHENUM", id.Bus);
        Assert.Null(id.Vid);
        Assert.Null(id.Pid);
        Assert.Null(id.SerialNumber);
        Assert.False(id.IsUsb);
    }

    [Fact]
    public void Built_in_port_has_no_usb_ids()
    {
        var id = PnpDeviceIdParser.Parse(@"ACPI\PNP0501\1");

        Assert.Equal("ACPI", id.Bus);
        Assert.Null(id.VidPid);
        Assert.Null(id.SerialNumber);
        Assert.Equal("1", id.InstanceId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("USB")]
    [InlineData(@"USB\")]
    [InlineData(@"\\\")]
    [InlineData(@"USB\VID_12&PID_34\X")]
    public void Malformed_input_does_not_throw(string? input)
    {
        var id = PnpDeviceIdParser.Parse(input);

        Assert.Null(id.VidPid);
        Assert.Null(id.SerialNumber);
    }

    [Fact]
    public void Serial_of_composite_interface_comes_from_parent()
    {
        var child = PnpDeviceIdParser.Parse(@"USB\VID_303A&PID_1001&MI_00\6&AB12CD34&0&0000");

        var serial = PnpDeviceIdParser.SerialFromParent(child, @"USB\VID_303A&PID_1001\AA:BB:CC:DD:EE:FF");

        Assert.Equal("AA:BB:CC:DD:EE:FF", serial);
    }

    [Fact]
    public void Parent_that_is_a_hub_gives_no_serial()
    {
        var child = PnpDeviceIdParser.Parse(@"USB\VID_1A86&PID_7523\5&1A2B3C4D&0&2");

        Assert.Null(PnpDeviceIdParser.SerialFromParent(child, @"USB\VID_0BDA&PID_5411\HUBSERIAL01"));
        Assert.Null(PnpDeviceIdParser.SerialFromParent(child, @"USB\ROOT_HUB30\4&1A2B3C4D&0&0"));
        Assert.Null(PnpDeviceIdParser.SerialFromParent(child, null));
    }
}
