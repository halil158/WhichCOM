using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Windows.Widgets.Providers;

namespace WhichCOM.WidgetProvider;

/// <summary>State of one widget the user has pinned to the widget board.</summary>
internal sealed class WidgetInstance(string id, string definitionId)
{
    public string Id { get; } = id;

    public string DefinitionId { get; } = definitionId;

    public bool IsActive { get; set; }

    public bool InCustomization { get; set; }
}

/// <summary>
/// COM object the widget host talks to. Data is only collected between Activate and Deactivate,
/// that is while the widget board is visible.
/// </summary>
internal sealed class WidgetProvider : IWidgetProvider, IWidgetProvider2
{
    public const string HelloDefinitionId = "WhichCOM_Hello";

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(4);

    private readonly ConcurrentDictionary<string, WidgetInstance> _widgets = new(StringComparer.Ordinal);
    private readonly ManualResetEventSlim _noWidgetsLeft = new(false);
    private readonly Lock _timerGate = new();
    private Timer? _timer;

    public WidgetProvider()
    {
        RestoreWidgets();
    }

    /// <summary>Set when the user has removed the last widget; the process can exit then.</summary>
    public WaitHandle NoWidgetsLeft => _noWidgetsLeft.WaitHandle;

    public bool HasWidgets => !_widgets.IsEmpty;

    public void CreateWidget(WidgetContext widgetContext)
    {
        var widget = new WidgetInstance(widgetContext.Id, widgetContext.DefinitionId);
        _widgets[widget.Id] = widget;
        _noWidgetsLeft.Reset();

        Log.Info($"CreateWidget {widget.DefinitionId} {widget.Id}");
        Update(widget);
    }

    public void DeleteWidget(string widgetId, string customState)
    {
        _widgets.TryRemove(widgetId, out _);
        Log.Info($"DeleteWidget {widgetId}");

        SyncTimer();
        if (_widgets.IsEmpty)
        {
            _noWidgetsLeft.Set();
        }
    }

    public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        Log.Info($"OnActionInvoked {actionInvokedArgs.Verb}");
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        if (_widgets.TryGetValue(contextChangedArgs.WidgetContext.Id, out var widget))
        {
            Update(widget);
        }
    }

    public void OnCustomizationRequested(WidgetCustomizationRequestedArgs customizationInvokedArgs)
    {
        Log.Info($"OnCustomizationRequested {customizationInvokedArgs.WidgetContext.Id}");
    }

    public void Activate(WidgetContext widgetContext)
    {
        // The host may activate a widget it created while this process was not running.
        var widget = _widgets.GetOrAdd(
            widgetContext.Id,
            id => new WidgetInstance(id, widgetContext.DefinitionId));

        widget.IsActive = true;
        _noWidgetsLeft.Reset();
        Log.Info($"Activate {widget.DefinitionId} {widget.Id}");

        Update(widget);
        SyncTimer();
    }

    public void Deactivate(string widgetId)
    {
        if (_widgets.TryGetValue(widgetId, out var widget))
        {
            widget.IsActive = false;
        }

        Log.Info($"Deactivate {widgetId}");
        SyncTimer();
    }

    private void RestoreWidgets()
    {
        try
        {
            // The list is null, not empty, when no widget is pinned.
            foreach (var info in WidgetManager.GetDefault().GetWidgetInfos() ?? [])
            {
                var context = info.WidgetContext;
                _widgets[context.Id] = new WidgetInstance(context.Id, context.DefinitionId);
            }

            Log.Info($"Restored {_widgets.Count} widget(s)");
        }
        catch (Exception ex)
        {
            Log.Error("Could not restore widgets", ex);
        }
    }

    // The timer exists only while at least one widget is active.
    private void SyncTimer()
    {
        lock (_timerGate)
        {
            var anyActive = _widgets.Values.Any(widget => widget.IsActive);

            if (anyActive && _timer is null)
            {
                _timer = new Timer(_ => RefreshActive(), null, RefreshInterval, RefreshInterval);
                Log.Info("Refresh started");
            }
            else if (!anyActive && _timer is not null)
            {
                _timer.Dispose();
                _timer = null;
                Log.Info("Refresh stopped");
            }
        }
    }

    private void RefreshActive()
    {
        foreach (var widget in _widgets.Values.Where(widget => widget.IsActive))
        {
            Update(widget);
        }
    }

    private static void Update(WidgetInstance widget)
    {
        try
        {
            var data = new JsonObject
            {
                ["title"] = "Hello from WhichCOM",
                ["message"] = "The widget provider is installed and running.",
                ["updated"] = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            };

            var options = new WidgetUpdateRequestOptions(widget.Id)
            {
                Template = TemplateStore.Get("Hello"),
                Data = data.ToJsonString(),
                CustomState = string.Empty,
            };

            WidgetManager.GetDefault().UpdateWidget(options);
        }
        catch (Exception ex)
        {
            Log.Error($"Update failed for {widget.Id}", ex);
        }
    }
}
