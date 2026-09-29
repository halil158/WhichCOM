using System.Reflection;
using WhichCOM.Core;
using WhichCOM.Core.Settings;

namespace WhichCOM.Cli;

internal sealed class ComlsApp
{
    public const int ExitOk = 0;
    public const int ExitNoPorts = 1;
    public const int ExitUsage = 2;
    public const int ExitFailure = 3;

    private const string HelpText = """
        comls - list the serial (COM) ports of this computer

        Usage:
          comls                  List ports as a table ('*' marks the most recently plugged port)
          comls --json           List ports as JSON
          comls --latest         Print only the name of the most recently plugged port
          comls --match <text>   Keep ports whose alias, chip type or description contains <text>
          comls --all            Include Bluetooth serial ports

        Options can be combined, e.g. 'comls --latest --match esp32'.

        Exit codes: 0 = ok, 1 = no matching port, 2 = invalid arguments, 3 = error.
        """;

    private readonly Func<bool, IReadOnlyList<SerialPortInfo>> _scan;
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    /// <param name="scan">Returns the ports; the argument tells whether hidden ports are included.</param>
    public ComlsApp(Func<bool, IReadOnlyList<SerialPortInfo>> scan, TextWriter output, TextWriter error)
    {
        _scan = scan;
        _output = output;
        _error = error;
    }

    public static ComlsApp CreateDefault(TextWriter output, TextWriter error)
    {
        var store = new SettingsStore();
        return new ComlsApp(
            includeHidden =>
            {
                var settings = store.Load();
                if (store.LastError is not null)
                {
                    error.WriteLine($"comls: ignoring {SettingsStore.FileName}: {store.LastError}");
                }

                return SerialPortScanner.CreateDefault(store.Directory).Scan(settings, includeHidden);
            },
            output,
            error);
    }

    public int Run(IReadOnlyList<string> args)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            _error.WriteLine($"comls: {options.Error}");
            _error.WriteLine("Try 'comls --help'.");
            return ExitUsage;
        }

        switch (options.Command)
        {
            case CliCommand.Help:
                _output.WriteLine(HelpText);
                return ExitOk;

            case CliCommand.Version:
                _output.WriteLine($"comls {Version()}");
                return ExitOk;
        }

        var ports = _scan(options.All);
        if (options.Match is not null)
        {
            ports = PortQuery.Match(ports, options.Match);
        }

        if (options.Command == CliCommand.Latest)
        {
            return WriteLatest(ports, options);
        }

        _output.Write(options.Json ? PortRenderer.Json(ports) : PortRenderer.Table(ports));
        return ports.Count > 0 ? ExitOk : ExitNoPorts;
    }

    private int WriteLatest(IReadOnlyList<SerialPortInfo> ports, CliOptions options)
    {
        var latest = PortQuery.Latest(ports);
        if (latest is null)
        {
            // Keep stdout empty so that $(comls --latest) expands to nothing.
            _error.WriteLine("comls: no serial port found.");
            return ExitNoPorts;
        }

        _output.Write(options.Json ? PortRenderer.Json([latest]) : latest.PortName + Environment.NewLine);
        return ExitOk;
    }

    private static string Version()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

        // Drop the source revision suffix ("+<commit>").
        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }
}
