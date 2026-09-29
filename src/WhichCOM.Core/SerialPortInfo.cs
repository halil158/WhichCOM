namespace WhichCOM.Core;

public sealed record SerialPortInfo
{
    /// <summary>Port name as used by tools, e.g. "COM3".</summary>
    public required string PortName { get; init; }

    public required int PortNumber { get; init; }

    /// <summary>Device name without the "(COMx)" suffix.</summary>
    public required string Description { get; init; }

    public required string PnpDeviceId { get; init; }

    public required string Bus { get; init; }

    public string? Vid { get; init; }

    public string? Pid { get; init; }

    public string? SerialNumber { get; init; }

    /// <summary>Physical USB location, stable for a given socket. Used to identify devices without a serial number.</summary>
    public string? LocationPath { get; init; }

    public string? Manufacturer { get; init; }

    public string? ChipLabel { get; init; }

    public IReadOnlyList<string> ChipTags { get; init; } = [];

    public string? Alias { get; init; }

    /// <summary>When the device was last plugged in, as recorded by Windows.</summary>
    public DateTimeOffset? LastArrival { get; init; }

    public string? VidPid => Vid is not null && Pid is not null ? $"{Vid}:{Pid}" : null;

    public bool IsUsb => VidPid is not null;

    public bool IsBluetooth => Bus.StartsWith("BTH", StringComparison.OrdinalIgnoreCase);

    /// <summary>Best short label for the device: alias, then chip type, then the device description.</summary>
    public string DisplayLabel => Alias ?? ChipLabel ?? Description;
}
