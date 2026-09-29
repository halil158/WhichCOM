namespace WhichCOM.Core.Tests;

public class PortQueryTests
{
    private static readonly DateTimeOffset Noon = TestPorts.Noon;

    [Fact]
    public void Latest_is_the_most_recently_plugged_usb_port()
    {
        SerialPortInfo[] ports =
        [
            TestPorts.Ch340(3, Noon.AddMinutes(-1)),
            TestPorts.Esp32(7, Noon.AddHours(-2)),
        ];

        Assert.Equal("COM3", PortQuery.Latest(ports)?.PortName);
    }

    [Fact]
    public void Latest_prefers_usb_over_built_in_ports()
    {
        SerialPortInfo[] ports =
        [
            TestPorts.BuiltIn(Noon),
            TestPorts.Ch340(3, Noon.AddDays(-1)),
        ];

        Assert.Equal("COM3", PortQuery.Latest(ports)?.PortName);
    }

    [Fact]
    public void Latest_falls_back_to_highest_port_number_without_arrival_times()
    {
        SerialPortInfo[] ports = [TestPorts.Ch340(3), TestPorts.Esp32(7)];

        Assert.Equal("COM7", PortQuery.Latest(ports)?.PortName);
    }

    [Fact]
    public void Latest_returns_built_in_port_when_it_is_the_only_one()
    {
        Assert.Equal("COM1", PortQuery.Latest([TestPorts.BuiltIn()])?.PortName);
    }

    [Fact]
    public void Latest_of_nothing_is_null()
    {
        Assert.Null(PortQuery.Latest([]));
    }

    [Theory]
    [InlineData("esp32", "COM7")]
    [InlineData("ESP32", "COM7")]
    [InlineData("ch34", "COM3")]
    [InlineData("1a86", "COM3")]
    [InlineData("sensor", "COM7")]
    [InlineData("com3", "COM3")]
    [InlineData("usb", "COM3,COM7")]
    [InlineData("", "COM1,COM3,COM7")]
    [InlineData("nothing-like-this", "")]
    public void Match_searches_alias_chip_tags_and_description(string term, string expected)
    {
        SerialPortInfo[] ports =
        [
            TestPorts.BuiltIn(),
            TestPorts.Ch340(),
            TestPorts.Esp32(alias: "Sensor board"),
        ];

        var matched = PortQuery.Match(ports, term).Select(port => port.PortName);

        Assert.Equal(expected, string.Join(',', matched));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(119, true)]
    [InlineData(120, true)]
    [InlineData(121, false)]
    [InlineData(-5, false)]
    public void Is_new_within_the_window(int secondsAgo, bool expected)
    {
        var port = TestPorts.Ch340(arrival: Noon.AddSeconds(-secondsAgo));

        Assert.Equal(expected, PortQuery.IsNew(port, Noon, TimeSpan.FromSeconds(120)));
    }

    [Fact]
    public void Ports_without_arrival_time_or_usb_ids_are_never_new()
    {
        var window = TimeSpan.FromSeconds(120);

        Assert.False(PortQuery.IsNew(TestPorts.Ch340(), Noon, window));
        Assert.False(PortQuery.IsNew(TestPorts.BuiltIn(Noon), Noon, window));
    }
}
