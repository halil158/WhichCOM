using System.Runtime.InteropServices;

namespace WhichCOM.Core.Devices;

/// <summary>Reads device node properties with the Configuration Manager API (cfgmgr32).</summary>
public sealed unsafe partial class CfgMgrDeviceNodeReader : IDeviceNodeReader
{
    private const uint Success = 0;
    private const uint BufferSmall = 0x1A;

    private const uint TypeFileTime = 0x10;
    private const uint TypeString = 0x12;
    private const uint TypeStringList = 0x2012;

    private const int MaxDeviceIdLength = 200;

    private static readonly DevPropKey LastArrivalDate =
        new(new Guid("83da6326-97a6-4088-9453-a1923f573b29"), 102);

    private static readonly DevPropKey LocationPaths =
        new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 37);

    public DeviceNodeInfo Read(string pnpDeviceId)
    {
        if (string.IsNullOrWhiteSpace(pnpDeviceId)
            || CM_Locate_DevNode(out var node, pnpDeviceId, 0) != Success)
        {
            return DeviceNodeInfo.Empty;
        }

        return new DeviceNodeInfo
        {
            LastArrival = ReadFileTime(node, LastArrivalDate),
            LocationPath = ReadString(node, LocationPaths),
            ParentDeviceId = ReadParentId(node),
        };
    }

    private static DateTimeOffset? ReadFileTime(uint node, DevPropKey key)
    {
        long value = 0;
        uint size = sizeof(long);

        if (CM_Get_DevNode_Property(node, key, out var type, (byte*)&value, ref size, 0) != Success
            || type != TypeFileTime
            || value <= 0)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromFileTime(value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    // For a string list the first entry is returned.
    private static string? ReadString(uint node, DevPropKey key)
    {
        uint size = 0;
        var status = CM_Get_DevNode_Property(node, key, out _, null, ref size, 0);
        if (status != BufferSmall || size == 0)
        {
            return null;
        }

        var buffer = new byte[size];
        uint type;
        fixed (byte* pointer = buffer)
        {
            if (CM_Get_DevNode_Property(node, key, out type, pointer, ref size, 0) != Success)
            {
                return null;
            }
        }

        if (type != TypeString && type != TypeStringList)
        {
            return null;
        }

        var text = MemoryMarshal.Cast<byte, char>(buffer.AsSpan(0, (int)size));
        var end = text.IndexOf('\0');
        var first = end >= 0 ? text[..end] : text;
        return first.IsEmpty ? null : first.ToString();
    }

    private static string? ReadParentId(uint node)
    {
        if (CM_Get_Parent(out var parent, node, 0) != Success)
        {
            return null;
        }

        var buffer = stackalloc char[MaxDeviceIdLength + 1];
        if (CM_Get_Device_ID(parent, buffer, MaxDeviceIdLength + 1, 0) != Success)
        {
            return null;
        }

        return new string(buffer);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct DevPropKey(Guid formatId, uint propertyId)
    {
        public readonly Guid FormatId = formatId;
        public readonly uint PropertyId = propertyId;
    }

    [LibraryImport("cfgmgr32.dll", EntryPoint = "CM_Locate_DevNodeW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint CM_Locate_DevNode(out uint node, string deviceId, uint flags);

    [LibraryImport("cfgmgr32.dll", EntryPoint = "CM_Get_DevNode_PropertyW")]
    private static partial uint CM_Get_DevNode_Property(
        uint node,
        in DevPropKey key,
        out uint propertyType,
        byte* buffer,
        ref uint bufferSize,
        uint flags);

    [LibraryImport("cfgmgr32.dll")]
    private static partial uint CM_Get_Parent(out uint parent, uint node, uint flags);

    [LibraryImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_IDW")]
    private static partial uint CM_Get_Device_ID(uint node, char* buffer, uint bufferLength, uint flags);
}
