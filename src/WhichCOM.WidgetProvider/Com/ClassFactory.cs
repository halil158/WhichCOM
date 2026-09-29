using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;
using WinRT;

namespace WhichCOM.WidgetProvider.Com;

[ComImport]
[ComVisible(false)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("00000001-0000-0000-C000-000000000046")]
internal interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(IntPtr outer, ref Guid interfaceId, out IntPtr instance);

    [PreserveSig]
    int LockServer(bool @lock);
}

/// <summary>Hands the single widget provider instance to the widget host.</summary>
[ComVisible(true)]
internal sealed class WidgetProviderFactory(IWidgetProvider provider) : IClassFactory
{
    private const int Ok = 0;
    private const int ClassNoAggregation = unchecked((int)0x80040110);
    private const int NoInterface = unchecked((int)0x80004002);

    private static readonly Guid Unknown = new("00000000-0000-0000-C000-000000000046");

    public int CreateInstance(IntPtr outer, ref Guid interfaceId, out IntPtr instance)
    {
        instance = IntPtr.Zero;

        if (outer != IntPtr.Zero)
        {
            return ClassNoAggregation;
        }

        if (interfaceId != typeof(IWidgetProvider).GUID && interfaceId != Unknown)
        {
            return NoInterface;
        }

        instance = MarshalInspectable<IWidgetProvider>.FromManaged(provider);
        ReportInterfaces(instance);
        return Ok;
    }

    // The widget board asks for these interfaces when it needs them. One that is missing here
    // explains a feature that silently does nothing, e.g. customization.
    private static void ReportInterfaces(IntPtr instance)
    {
        if (Interlocked.Exchange(ref _reported, 1) == 1)
        {
            return;
        }

        (string Name, Guid Id)[] interfaces =
        [
            (nameof(IWidgetProvider2), typeof(IWidgetProvider2).GUID),
            (nameof(IWidgetProviderAnalytics), typeof(IWidgetProviderAnalytics).GUID),
        ];

        foreach (var (name, id) in interfaces)
        {
            var result = Marshal.QueryInterface(instance, in id, out var pointer);
            if (pointer != IntPtr.Zero)
            {
                Marshal.Release(pointer);
            }

            if (result != Ok)
            {
                Log.Error($"The provider object does not offer {name} (0x{result:X8})");
            }
        }
    }

    private static int _reported;

    public int LockServer(bool @lock) => Ok;
}

internal static partial class ComServer
{
    private const uint LocalServer = 0x4;
    private const uint MultipleUse = 0x1;

    /// <summary>Registers the class factory. Returns the cookie needed to revoke it.</summary>
    public static uint Register(Guid classId, IClassFactory factory)
    {
        Marshal.ThrowExceptionForHR(CoRegisterClassObject(classId, factory, LocalServer, MultipleUse, out var cookie));
        return cookie;
    }

    public static void Revoke(uint cookie) => _ = CoRevokeClassObject(cookie);

    [DllImport("ole32.dll")]
    private static extern int CoRegisterClassObject(
        [MarshalAs(UnmanagedType.LPStruct)] Guid classId,
        [MarshalAs(UnmanagedType.IUnknown)] object factory,
        uint context,
        uint flags,
        out uint cookie);

    [DllImport("ole32.dll")]
    private static extern int CoRevokeClassObject(uint cookie);
}
