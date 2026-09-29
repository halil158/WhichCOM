using System.Text;
using System.Text.Json;
using WhichCOM.Core;

namespace WhichCOM.Cli;

internal static class PortRenderer
{
    private const string Empty = "-";

    private static readonly string[] Headers = ["PORT", "ALIAS", "CHIP", "VID:PID", "SERIAL", "DESCRIPTION"];

    /// <summary>Plain text table. The most recently plugged port is marked with '*'.</summary>
    public static string Table(IReadOnlyList<SerialPortInfo> ports)
    {
        if (ports.Count == 0)
        {
            return "No serial ports found." + Environment.NewLine;
        }

        var latest = PortQuery.Latest(ports);
        var rows = ports
            .Select(port => new[]
            {
                port.PortName + (ReferenceEquals(port, latest) ? "*" : string.Empty),
                port.Alias ?? Empty,
                port.ChipLabel ?? Empty,
                port.VidPid ?? Empty,
                port.SerialNumber ?? Empty,
                port.Description,
            })
            .ToList();

        var widths = Headers
            .Select((header, column) => Math.Max(header.Length, rows.Max(row => row[column].Length)))
            .ToArray();

        var builder = new StringBuilder();
        AppendRow(builder, Headers, widths);
        foreach (var row in rows)
        {
            AppendRow(builder, row, widths);
        }

        return builder.ToString();
    }

    public static string Json(IReadOnlyList<SerialPortInfo> ports) =>
        JsonSerializer.Serialize(ports.ToList(), WhichComJsonContext.Default.ListSerialPortInfo) + Environment.NewLine;

    private static void AppendRow(StringBuilder builder, string[] cells, int[] widths)
    {
        for (var column = 0; column < cells.Length; column++)
        {
            var last = column == cells.Length - 1;
            builder.Append(last ? cells[column] : cells[column].PadRight(widths[column] + 2));
        }

        builder.AppendLine();
    }
}
