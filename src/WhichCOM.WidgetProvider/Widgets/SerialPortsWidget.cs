using System.Diagnostics;
using WhichCOM.Core.Cards;

namespace WhichCOM.WidgetProvider.Widgets;

internal sealed class SerialPortsWidget : IWidgetKind
{
    public static readonly TimeSpan CopiedFeedback = TimeSpan.FromSeconds(2);

    public string DefinitionId => "WhichCOM_SerialPorts";

    public WidgetContent Render(WidgetInstance widget, Snapshot snapshot)
    {
        var copied = snapshot.Now < widget.CopiedUntil ? widget.CopiedPort : null;

        var data = SerialPortsCard.BuildData(
            snapshot.Ports,
            widget.CardSize,
            snapshot.Strings,
            snapshot.Now,
            TimeSpan.FromSeconds(Math.Max(0, snapshot.Settings.NewBadgeSeconds)),
            copied);

        var template = widget.CardSize switch
        {
            CardSize.Small => "SerialPorts.small",
            CardSize.Large => "SerialPorts.large",
            _ => "SerialPorts.medium",
        };

        return new WidgetContent(TemplateStore.Get(template), data.ToJsonString());
    }

    public bool HandleAction(WidgetInstance widget, string verb, string data, Snapshot snapshot)
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
