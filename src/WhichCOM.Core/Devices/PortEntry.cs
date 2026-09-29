namespace WhichCOM.Core.Devices;

/// <summary>A device of the Ports class, as reported by the system before any parsing.</summary>
public sealed record PortEntry(string Name, string PnpDeviceId, string? Manufacturer);

/// <summary>Device properties that are not part of the device ID.</summary>
public sealed record DeviceNodeInfo
{
    public static DeviceNodeInfo Empty { get; } = new();

    public DateTimeOffset? LastArrival { get; init; }

    public string? LocationPath { get; init; }

    public string? ParentDeviceId { get; init; }
}

public interface IPortEntrySource
{
    IReadOnlyList<PortEntry> GetPortEntries();
}

public interface IDeviceNodeReader
{
    /// <summary>Never throws; returns <see cref="DeviceNodeInfo.Empty"/> when the device is unknown.</summary>
    DeviceNodeInfo Read(string pnpDeviceId);
}
