namespace WhichCOM.Core.Settings;

public static class AliasResolver
{
    /// <summary>
    /// Finds the nickname of a device. A serial number match wins over a VID:PID + location match,
    /// which wins over a VID:PID-only entry.
    /// </summary>
    public static string? Resolve(
        IEnumerable<AliasEntry> aliases,
        string? vidPid,
        string? serialNumber,
        string? locationPath)
    {
        string? byLocation = null;
        string? byVidPid = null;

        foreach (var alias in aliases)
        {
            if (string.IsNullOrWhiteSpace(alias.Name))
            {
                continue;
            }

            var vidPidMatches = Same(alias.VidPid, vidPid);

            if (HasValue(alias.Serial))
            {
                if (Same(alias.Serial, serialNumber) && (!HasValue(alias.VidPid) || vidPidMatches))
                {
                    return alias.Name.Trim();
                }

                continue;
            }

            if (!vidPidMatches)
            {
                continue;
            }

            if (HasValue(alias.Location))
            {
                if (Same(alias.Location, locationPath))
                {
                    byLocation ??= alias.Name.Trim();
                }
            }
            else
            {
                byVidPid ??= alias.Name.Trim();
            }
        }

        return byLocation ?? byVidPid;
    }

    /// <summary>Adds, replaces or (with an empty name) removes the nickname of a port's device.</summary>
    public static bool SetAlias(WhichComSettings settings, SerialPortInfo port, string? name)
    {
        var entry = CreateEntry(port, name ?? string.Empty);
        if (entry is null)
        {
            return false;
        }

        settings.Aliases.RemoveAll(existing => IdentifiesSameDevice(existing, entry));

        if (HasValue(entry.Name))
        {
            settings.Aliases.Add(entry);
        }

        return true;
    }

    /// <summary>Returns null when the device cannot be identified reliably.</summary>
    public static AliasEntry? CreateEntry(SerialPortInfo port, string name)
    {
        if (HasValue(port.SerialNumber))
        {
            return new AliasEntry { Serial = port.SerialNumber, VidPid = port.VidPid, Name = name.Trim() };
        }

        if (HasValue(port.VidPid) && HasValue(port.LocationPath))
        {
            return new AliasEntry { VidPid = port.VidPid, Location = port.LocationPath, Name = name.Trim() };
        }

        return null;
    }

    private static bool IdentifiesSameDevice(AliasEntry a, AliasEntry b) =>
        Same(a.Serial ?? string.Empty, b.Serial ?? string.Empty)
        && Same(a.VidPid ?? string.Empty, b.VidPid ?? string.Empty)
        && Same(a.Location ?? string.Empty, b.Location ?? string.Empty);

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool Same(string? a, string? b) =>
        a is not null && b is not null
        && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
