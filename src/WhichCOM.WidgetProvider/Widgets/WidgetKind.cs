using Microsoft.Windows.Widgets;
using WhichCOM.Core;
using WhichCOM.Core.Cards;
using WhichCOM.Core.Settings;
using WhichCOM.Core.Status;
using WhichCOM.WidgetProvider.Status;

namespace WhichCOM.WidgetProvider.Widgets;

/// <summary>State of one widget the user has pinned to the widget board.</summary>
internal sealed class WidgetInstance(string id, string definitionId)
{
    public string Id { get; } = id;

    public string DefinitionId { get; } = definitionId;

    public Lock Gate { get; } = new();

    public WidgetSize Size { get; set; } = WidgetSize.Medium;

    public bool IsActive { get; set; }

    /// <summary>True while the widget shows the card for editing nicknames.</summary>
    public bool InCustomization { get; set; }

    /// <summary>Port whose copy button shows a confirmation until <see cref="CopiedUntil"/>.</summary>
    public string? CopiedPort { get; set; }

    public DateTimeOffset CopiedUntil { get; set; }

    /// <summary>Content sent last, to skip updates that change nothing.</summary>
    public WidgetContent? LastSent { get; set; }

    public CardSize CardSize => Size switch
    {
        WidgetSize.Small => CardSize.Small,
        WidgetSize.Large => CardSize.Large,
        _ => CardSize.Medium,
    };
}

internal sealed record WidgetContent(string Template, string Data);

/// <summary>What the system looks like at one moment. Shared by all widgets of one refresh.</summary>
internal sealed class Snapshot(SettingsStore store, DateTimeOffset now)
{
    private readonly Lazy<WhichComSettings> _settings = new(store.Load);
    private Lazy<SystemStatus>? _status;
    private Lazy<IReadOnlyList<SerialPortInfo>>? _ports;

    public DateTimeOffset Now { get; } = now;

    public SystemStatus Status =>
        (_status ??= new Lazy<SystemStatus>(() => SystemStatusReader.Read(Settings.UseWifiApi))).Value;

    public WhichComSettings Settings => _settings.Value;

    public CardStrings Strings => CardStrings.For(Platform.UiLanguage.Resolve(Settings.Language));

    public IReadOnlyList<SerialPortInfo> Ports =>
        (_ports ??= new Lazy<IReadOnlyList<SerialPortInfo>>(
            () => SerialPortScanner.CreateDefault(store.Directory).Scan(Settings))).Value;
}

/// <summary>One kind of widget, as declared in the package manifest.</summary>
internal interface IWidgetKind
{
    string DefinitionId { get; }

    WidgetContent Render(WidgetInstance widget, Snapshot snapshot);

    /// <summary>Handles a button. Returns true when the widget has to be drawn again.</summary>
    bool HandleAction(WidgetInstance widget, string verb, string data, Snapshot snapshot);
}
