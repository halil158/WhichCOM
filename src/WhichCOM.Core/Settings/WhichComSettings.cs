namespace WhichCOM.Core.Settings;

/// <summary>User settings shared by the widget provider and the comls tool.</summary>
public sealed class WhichComSettings
{
    public List<AliasEntry> Aliases { get; set; } = [];

    /// <summary>Bluetooth serial ports are always present and rarely the device being looked for.</summary>
    public bool ShowBluetoothPorts { get; set; }

    /// <summary>How long a freshly plugged port is marked as new.</summary>
    public int NewBadgeSeconds { get; set; } = 120;

    /// <summary>Refresh interval of the widgets while the widget board is visible.</summary>
    public int RefreshSeconds { get; set; } = 4;
}

/// <summary>
/// A nickname for a device. Identify the device by <see cref="Serial"/> when it has one, otherwise
/// by <see cref="VidPid"/> together with <see cref="Location"/>.
/// </summary>
public sealed class AliasEntry
{
    public string? Serial { get; set; }

    /// <summary>"VID:PID" in hex, e.g. "1A86:7523".</summary>
    public string? VidPid { get; set; }

    /// <summary>USB location path of the socket the device is plugged into.</summary>
    public string? Location { get; set; }

    public string Name { get; set; } = string.Empty;
}
