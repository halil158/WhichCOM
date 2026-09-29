using WhichCOM.Core.Cards;

namespace WhichCOM.WidgetProvider.Widgets;

internal sealed class SerialPortsWidget : IWidgetKind
{
    public string DefinitionId => "WhichCOM_SerialPorts";

    public WidgetContent Render(WidgetInstance widget, Snapshot snapshot)
    {
        var data = SerialPortsCard.BuildData(
            snapshot.Ports,
            widget.CardSize,
            snapshot.Strings,
            snapshot.Now,
            PortActions.NewWindow(snapshot),
            PortActions.CopiedPort(widget, snapshot));

        var template = widget.CardSize switch
        {
            CardSize.Small => "SerialPorts.small",
            CardSize.Large => "SerialPorts.large",
            _ => "SerialPorts.medium",
        };

        return new WidgetContent(TemplateStore.Get(template), data.ToJsonString());
    }

    public bool HandleAction(WidgetInstance widget, string verb, string data, Snapshot snapshot) =>
        PortActions.Handle(widget, verb, data, snapshot);
}
