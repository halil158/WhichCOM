namespace WhichCOM.Core.Tests;

/// <summary>Made-up ports shared by the tests.</summary>
internal static class TestPorts
{
    public static readonly DateTimeOffset Noon = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public static SerialPortInfo BuiltIn(DateTimeOffset? arrival = null) => new()
    {
        PortName = "COM1",
        PortNumber = 1,
        Description = "Communications Port",
        PnpDeviceId = @"ACPI\PNP0501\1",
        Bus = "ACPI",
        LastArrival = arrival,
    };

    public static SerialPortInfo Ch340(int number = 3, DateTimeOffset? arrival = null) => new()
    {
        PortName = $"COM{number}",
        PortNumber = number,
        Description = "USB-SERIAL CH340",
        PnpDeviceId = @"USB\VID_1A86&PID_7523\5&1A2B3C4D&0&2",
        Bus = "USB",
        Vid = "1A86",
        Pid = "7523",
        ChipLabel = "CH340",
        ChipTags = ["ch34x"],
        LastArrival = arrival,
    };

    public static SerialPortInfo Esp32(int number = 7, DateTimeOffset? arrival = null, string? alias = null) => new()
    {
        PortName = $"COM{number}",
        PortNumber = number,
        Description = "USB Serial Device",
        PnpDeviceId = @"USB\VID_303A&PID_1001&MI_00\6&AB12CD34&0&0000",
        Bus = "USB",
        Vid = "303A",
        Pid = "1001",
        SerialNumber = "AA:BB:CC:DD:EE:FF",
        ChipLabel = "ESP32 native USB",
        ChipTags = ["esp32", "esp32-s3", "esp32-c3"],
        Alias = alias,
        LastArrival = arrival,
    };
}
