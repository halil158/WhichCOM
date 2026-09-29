namespace WhichCOM.Core;

public static class PortQuery
{
    /// <summary>
    /// The most recently plugged port. USB devices are preferred because built-in ports "arrive"
    /// at every boot. Returns null when there are no ports.
    /// </summary>
    public static SerialPortInfo? Latest(IEnumerable<SerialPortInfo> ports) =>
        ports
            .OrderByDescending(port => port.IsUsb)
            .ThenByDescending(port => port.LastArrival ?? DateTimeOffset.MinValue)
            .ThenByDescending(port => port.PortNumber)
            .FirstOrDefault();

    /// <summary>Case-insensitive search in the port name, alias, chip type and tags, and description.</summary>
    public static IReadOnlyList<SerialPortInfo> Match(IEnumerable<SerialPortInfo> ports, string text)
    {
        var term = text.Trim();
        return ports.Where(port => Matches(port, term)).ToList();
    }

    public static bool Matches(SerialPortInfo port, string term)
    {
        if (term.Length == 0)
        {
            return true;
        }

        return Contains(port.PortName, term)
            || Contains(port.Alias, term)
            || Contains(port.ChipLabel, term)
            || Contains(port.Description, term)
            || Contains(port.Manufacturer, term)
            || Contains(port.VidPid, term)
            || port.ChipTags.Any(tag => Contains(tag, term));
    }

    /// <summary>True when the port was plugged in within <paramref name="window"/> before <paramref name="now"/>.</summary>
    public static bool IsNew(SerialPortInfo port, DateTimeOffset now, TimeSpan window)
    {
        if (!port.IsUsb || port.LastArrival is not { } arrival)
        {
            return false;
        }

        var age = now - arrival;
        return age >= TimeSpan.Zero && age <= window;
    }

    private static bool Contains(string? value, string term) =>
        value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);
}
