using WhichCOM.Core.Cards;

namespace WhichCOM.WidgetProvider.Widgets;

internal sealed class SystemStatusWidget : IWidgetKind
{
    public string DefinitionId => "WhichCOM_SystemStatus";

    public WidgetContent Render(WidgetInstance widget, Snapshot snapshot)
    {
        // Only the large card shows serial ports, so only it reads them.
        if (widget.CardSize == CardSize.Large)
        {
            var everything = SystemStatusCard.BuildData(
                snapshot.Status,
                snapshot.Ports,
                snapshot.Strings,
                snapshot.Now,
                PortActions.NewWindow(snapshot),
                PortActions.CopiedPort(widget, snapshot));

            return new WidgetContent(TemplateStore.Get("SystemStatus.large"), everything.ToJsonString());
        }

        var data = SystemStatusCard.BuildData(snapshot.Status, snapshot.Strings);
        var template = widget.CardSize == CardSize.Small ? "SystemStatus.small" : "SystemStatus.medium";

        return new WidgetContent(TemplateStore.Get(template), data.ToJsonString());
    }

    public bool HandleAction(WidgetInstance widget, string verb, string data, Snapshot snapshot) =>
        PortActions.Handle(widget, verb, data, snapshot);
}
