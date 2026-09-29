namespace WhichCOM.Core.Status;

public enum LinkKind
{
    Ethernet,
    WiFi,
}

/// <summary>A physical network adapter and its connection.</summary>
public sealed record NetworkLink
{
    public required LinkKind Kind { get; init; }

    /// <summary>Connection name as shown by Windows, e.g. "Ethernet" or "Wi-Fi".</summary>
    public required string Name { get; init; }

    public bool IsConnected { get; init; }

    public long? SpeedBitsPerSecond { get; init; }

    public string? IPv4 { get; init; }

    /// <summary>Wi-Fi network name. Null for Ethernet, and when Windows does not reveal it.</summary>
    public string? Ssid { get; init; }

    /// <summary>Wi-Fi signal quality from 0 to 100.</summary>
    public int? SignalPercent { get; init; }
}

public sealed record SystemStatus
{
    public IReadOnlyList<NetworkLink> Links { get; init; } = [];

    public bool HasInternet { get; init; }

    /// <summary>Name of the default playback device, or null when there is none.</summary>
    public string? AudioOutput { get; init; }

    /// <summary>Name of the default recording device, or null when there is none.</summary>
    public string? AudioInput { get; init; }
}
