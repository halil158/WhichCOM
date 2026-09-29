using System.Text.RegularExpressions;

namespace WhichCOM.Core;

/// <summary>
/// The parts of a PnP device instance ID such as <c>USB\VID_10C4&amp;PID_EA60\0001</c>.
/// </summary>
public sealed record PnpDeviceId
{
    public required string Raw { get; init; }

    /// <summary>Enumerator name in upper case, e.g. USB, FTDIBUS, BTHENUM, ACPI.</summary>
    public required string Bus { get; init; }

    /// <summary>USB vendor ID as four upper-case hex digits, or null when the ID carries none.</summary>
    public string? Vid { get; init; }

    /// <summary>USB product ID as four upper-case hex digits, or null when the ID carries none.</summary>
    public string? Pid { get; init; }

    /// <summary>Interface number (MI_xx) when the port is one function of a composite USB device.</summary>
    public int? InterfaceNumber { get; init; }

    /// <summary>Channel letter of a multi-port FTDI chip (A, B, ...).</summary>
    public char? Channel { get; init; }

    /// <summary>Last segment of the ID, exactly as reported.</summary>
    public string? InstanceId { get; init; }

    /// <summary>
    /// Serial number reported by the device. Null when Windows generated the instance ID itself,
    /// which is the case for devices without a serial number and for composite device interfaces.
    /// </summary>
    public string? SerialNumber { get; init; }

    public string? VidPid => Vid is not null && Pid is not null ? $"{Vid}:{Pid}" : null;

    public bool IsUsb => Vid is not null && Pid is not null;
}

public static partial class PnpDeviceIdParser
{
    private const string FtdiBus = "FTDIBUS";

    [GeneratedRegex("VID_([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VidRegex();

    [GeneratedRegex("PID_([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PidRegex();

    [GeneratedRegex("MI_([0-9A-F]{2})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InterfaceRegex();

    public static PnpDeviceId Parse(string? pnpDeviceId)
    {
        var raw = (pnpDeviceId ?? string.Empty).Trim();
        var segments = raw.Split('\\', 3);

        var bus = segments[0].ToUpperInvariant();
        var device = segments.Length > 1 ? segments[1] : string.Empty;
        var instance = segments.Length > 2 && segments[2].Length > 0 ? segments[2] : null;

        var vid = MatchHex(VidRegex(), device);
        var pid = MatchHex(PidRegex(), device);

        if (bus == FtdiBus)
        {
            var (serial, channel) = ParseFtdiSerial(device);
            return new PnpDeviceId
            {
                Raw = raw,
                Bus = bus,
                Vid = vid,
                Pid = pid,
                Channel = channel,
                InstanceId = instance,
                SerialNumber = serial,
            };
        }

        int? interfaceNumber = null;
        var interfaceMatch = InterfaceRegex().Match(device);
        if (interfaceMatch.Success)
        {
            interfaceNumber = Convert.ToInt32(interfaceMatch.Groups[1].Value, 16);
        }

        // The instance segment is the device serial number only for a non-composite USB device
        // that reports one. Anything Windows generated contains '&' (e.g. "6&2a9e1f3b&0&2").
        string? serialNumber = null;
        if (vid is not null && pid is not null && interfaceNumber is null && IsDeviceSerial(instance))
        {
            serialNumber = instance;
        }

        return new PnpDeviceId
        {
            Raw = raw,
            Bus = bus,
            Vid = vid,
            Pid = pid,
            InterfaceNumber = interfaceNumber,
            InstanceId = instance,
            SerialNumber = serialNumber,
        };
    }

    /// <summary>
    /// Serial number of the USB device a composite interface belongs to, taken from the parent
    /// device ID. Returns null when the parent is a different device (e.g. a hub).
    /// </summary>
    public static string? SerialFromParent(PnpDeviceId child, string? parentDeviceId)
    {
        if (string.IsNullOrWhiteSpace(parentDeviceId) || !child.IsUsb)
        {
            return null;
        }

        var parent = Parse(parentDeviceId);
        return parent.Vid == child.Vid && parent.Pid == child.Pid ? parent.SerialNumber : null;
    }

    // FTDI's bus driver reports "VID_0403+PID_6001+<serial><channel>", where channel is the port
    // letter of the chip (always 'A' on single-port chips).
    private static (string? Serial, char? Channel) ParseFtdiSerial(string device)
    {
        var parts = device.Split('+');
        if (parts.Length < 3 || !IsDeviceSerial(parts[2]))
        {
            return (null, null);
        }

        var value = parts[2];
        var last = char.ToUpperInvariant(value[^1]);
        if (value.Length > 1 && last is >= 'A' and <= 'H')
        {
            return (value[..^1], last);
        }

        return (value, null);
    }

    private static bool IsDeviceSerial(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains('&');

    private static string? MatchHex(Regex regex, string input)
    {
        var match = regex.Match(input);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }
}
