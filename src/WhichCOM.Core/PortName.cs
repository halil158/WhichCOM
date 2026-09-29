using System.Globalization;
using System.Text.RegularExpressions;

namespace WhichCOM.Core;

public static partial class PortName
{
    [GeneratedRegex(@"\(COM(\d+)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ComRegex();

    /// <summary>
    /// Splits a device name such as "USB-SERIAL CH340 (COM3)" into the port and the description.
    /// Returns false for names without a COM port, e.g. printer ports.
    /// </summary>
    public static bool TryParse(string? deviceName, out string portName, out int portNumber, out string description)
    {
        portName = string.Empty;
        portNumber = 0;
        description = string.Empty;

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return false;
        }

        var matches = ComRegex().Matches(deviceName);
        if (matches.Count == 0)
        {
            return false;
        }

        var match = matches[^1];
        if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out portNumber))
        {
            return false;
        }

        portName = $"COM{portNumber}";
        description = deviceName.Remove(match.Index, match.Length).Trim();
        return true;
    }
}
