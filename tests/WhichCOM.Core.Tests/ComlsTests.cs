using System.Text.Json;
using WhichCOM.Cli;

namespace WhichCOM.Core.Tests;

public class ComlsTests
{
    private static readonly DateTimeOffset Noon = TestPorts.Noon;

    private static readonly SerialPortInfo[] Ports =
    [
        TestPorts.BuiltIn(),
        TestPorts.Ch340(3, Noon.AddHours(-1)),
        TestPorts.Esp32(7, Noon, alias: "Sensor board"),
    ];

    [Fact]
    public void Parses_options()
    {
        Assert.Equal(CliCommand.List, CliOptions.Parse([]).Command);
        Assert.True(CliOptions.Parse(["--json"]).Json);
        Assert.True(CliOptions.Parse(["--all"]).All);
        Assert.Equal(CliCommand.Latest, CliOptions.Parse(["--latest"]).Command);
        Assert.Equal("esp32", CliOptions.Parse(["--match", "esp32"]).Match);
        Assert.Equal("esp32", CliOptions.Parse(["--match=esp32"]).Match);
        Assert.Equal(CliCommand.Help, CliOptions.Parse(["--help"]).Command);
        Assert.Equal(CliCommand.Version, CliOptions.Parse(["--version"]).Command);

        var combined = CliOptions.Parse(["--latest", "--match", "esp32", "--json"]);
        Assert.Equal(CliCommand.Latest, combined.Command);
        Assert.Equal("esp32", combined.Match);
        Assert.True(combined.Json);
    }

    [Theory]
    [InlineData("--nope")]
    [InlineData("--match")]
    [InlineData("--match", "--json")]
    [InlineData("--match=")]
    [InlineData("esp32")]
    public void Reports_invalid_arguments(params string[] args)
    {
        Assert.NotNull(CliOptions.Parse(args).Error);

        var (exitCode, output, error) = Run(Ports, args);

        Assert.Equal(ComlsApp.ExitUsage, exitCode);
        Assert.Empty(output);
        Assert.Contains("comls:", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Lists_ports_as_a_table()
    {
        var (exitCode, output, _) = Run(Ports);

        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(ComlsApp.ExitOk, exitCode);
        Assert.Equal(4, lines.Length);
        Assert.StartsWith("PORT", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("COM1 ", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("COM3 ", lines[2], StringComparison.Ordinal);
        Assert.StartsWith("COM7*", lines[3], StringComparison.Ordinal);
        Assert.Contains("Sensor board", lines[3], StringComparison.Ordinal);
        Assert.Contains("303A:1001", lines[3], StringComparison.Ordinal);

        // Columns line up.
        Assert.Equal(
            lines[0].IndexOf("VID:PID", StringComparison.Ordinal),
            lines[3].IndexOf("303A:1001", StringComparison.Ordinal));
    }

    [Fact]
    public void Latest_prints_only_the_port_name()
    {
        var (exitCode, output, error) = Run(Ports, "--latest");

        Assert.Equal(ComlsApp.ExitOk, exitCode);
        Assert.Equal("COM7" + Environment.NewLine, output);
        Assert.Empty(error);
    }

    [Fact]
    public void Latest_can_be_combined_with_match()
    {
        var (_, output, _) = Run(Ports, "--latest", "--match", "ch340");

        Assert.Equal("COM3", output.Trim());
    }

    [Fact]
    public void Latest_without_ports_keeps_stdout_empty()
    {
        var (exitCode, output, error) = Run([], "--latest");

        Assert.Equal(ComlsApp.ExitNoPorts, exitCode);
        Assert.Empty(output);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Match_without_result_returns_no_ports_exit_code()
    {
        var (exitCode, output, _) = Run(Ports, "--match", "nothing-like-this");

        Assert.Equal(ComlsApp.ExitNoPorts, exitCode);
        Assert.Contains("No serial ports found", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_output_is_a_camel_case_array()
    {
        var (exitCode, output, _) = Run(Ports, "--json", "--match", "esp32");

        using var document = JsonDocument.Parse(output);
        var port = Assert.Single(document.RootElement.EnumerateArray().ToList());

        Assert.Equal(ComlsApp.ExitOk, exitCode);
        Assert.Equal("COM7", port.GetProperty("portName").GetString());
        Assert.Equal(7, port.GetProperty("portNumber").GetInt32());
        Assert.Equal("303A", port.GetProperty("vid").GetString());
        Assert.Equal("303A:1001", port.GetProperty("vidPid").GetString());
        Assert.Equal("Sensor board", port.GetProperty("alias").GetString());
        Assert.Equal("ESP32 native USB", port.GetProperty("chipLabel").GetString());
        Assert.True(port.GetProperty("isUsb").GetBoolean());
    }

    [Fact]
    public void Json_output_without_ports_is_an_empty_array()
    {
        var (exitCode, output, _) = Run([], "--json");

        using var document = JsonDocument.Parse(output);

        Assert.Equal(ComlsApp.ExitNoPorts, exitCode);
        Assert.Equal(0, document.RootElement.GetArrayLength());
    }

    [Fact]
    public void All_option_is_passed_to_the_scanner()
    {
        var requested = new List<bool>();
        var app = new ComlsApp(
            includeHidden =>
            {
                requested.Add(includeHidden);
                return Ports;
            },
            TextWriter.Null,
            TextWriter.Null);

        app.Run([]);
        app.Run(["--all"]);

        Assert.Equal([false, true], requested);
    }

    [Fact]
    public void Help_and_version_do_not_scan()
    {
        var app = new ComlsApp(_ => throw new InvalidOperationException("must not scan"), TextWriter.Null, TextWriter.Null);

        Assert.Equal(ComlsApp.ExitOk, app.Run(["--help"]));
        Assert.Equal(ComlsApp.ExitOk, app.Run(["--version"]));
    }

    private static (int ExitCode, string Output, string Error) Run(IReadOnlyList<SerialPortInfo> ports, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new ComlsApp(_ => ports, output, error).Run(args);

        return (exitCode, output.ToString(), error.ToString());
    }
}
