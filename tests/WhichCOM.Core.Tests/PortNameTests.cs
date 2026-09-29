namespace WhichCOM.Core.Tests;

public class PortNameTests
{
    [Theory]
    [InlineData("USB-SERIAL CH340 (COM3)", "COM3", 3, "USB-SERIAL CH340")]
    [InlineData("Silicon Labs CP210x USB to UART Bridge (COM12)", "COM12", 12, "Silicon Labs CP210x USB to UART Bridge")]
    [InlineData("USB Serial Device (com7)", "COM7", 7, "USB Serial Device")]
    [InlineData("(COM256)", "COM256", 256, "")]
    [InlineData("Dual port (A) adapter (COM5)", "COM5", 5, "Dual port (A) adapter")]
    public void Parses_port_and_description(string name, string port, int number, string description)
    {
        Assert.True(PortName.TryParse(name, out var portName, out var portNumber, out var text));

        Assert.Equal(port, portName);
        Assert.Equal(number, portNumber);
        Assert.Equal(description, text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ECP Printer Port (LPT1)")]
    [InlineData("Some device COM3")]
    [InlineData("Broken (COM)")]
    public void Rejects_names_without_com_port(string? name)
    {
        Assert.False(PortName.TryParse(name, out _, out _, out _));
    }
}
