using System.Diagnostics;
using WhichCOM.Core.Cards;

namespace WhichCOM.WidgetProvider.Widgets;

/// <summary>Buttons of the serial port rows. Shared by the widgets that show ports.</summary>
internal static class PortActions
{
    public static readonly TimeSpan CopiedFeedback = TimeSpan.FromSeconds(2);

    /// <summary>Handles a button. Returns true when the widget has to be drawn again.</summary>
    public static bool Handle(WidgetInstance widget, string verb, string data, Snapshot snapshot)
    {
        switch (verb)
        {
            case SerialPortsCard.CopyVerb:
                var port = SerialPortsCard.ReadPortFromAction(data);
                if (port is null || !Platform.Clipboard.SetText(port))
                {
                    Log.Error($"Copy failed (valid port: {port is not null})");
                    return false;
                }

                widget.CopiedPort = port;
                widget.CopiedUntil = snapshot.Now + CopiedFeedback;
                return true;

            case SerialPortsCard.DeviceManagerVerb:
                OpenDeviceManager();
                return false;

            default:
                return false;
        }
    }

    /// <summary>Port whose copy button currently shows the confirmation, if any.</summary>
    public static string? CopiedPort(WidgetInstance widget, Snapshot snapshot) =>
        snapshot.Now < widget.CopiedUntil ? widget.CopiedPort : null;

    public static TimeSpan NewWindow(Snapshot snapshot) =>
        TimeSpan.FromSeconds(Math.Max(0, snapshot.Settings.NewBadgeSeconds));

    private static void OpenDeviceManager()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("devmgmt.msc") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Error("Could not open Device Manager", ex);
        }
    }
}
