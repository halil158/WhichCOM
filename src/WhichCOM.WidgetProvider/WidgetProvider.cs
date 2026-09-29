using System.Collections.Concurrent;
using Microsoft.Windows.Widgets.Providers;
using WhichCOM.Core.Settings;
using WhichCOM.WidgetProvider.Widgets;

namespace WhichCOM.WidgetProvider;

/// <summary>
/// COM object the widget host talks to. Data is only collected between Activate and Deactivate,
/// that is while the widget board is visible.
/// </summary>
internal sealed class WidgetProvider : IWidgetProvider, IWidgetProvider2
{
    private const int MinRefreshSeconds = 2;
    private const int MaxRefreshSeconds = 60;

    private readonly Dictionary<string, IWidgetKind> _kinds;
    private readonly ConcurrentDictionary<string, WidgetInstance> _widgets = new(StringComparer.Ordinal);
    private readonly SettingsStore _settings = new();
    private readonly ManualResetEventSlim _noWidgetsLeft = new(false);
    private readonly Lock _timerGate = new();
    private Timer? _timer;
    private int _refreshing;

    public WidgetProvider()
    {
        IWidgetKind[] kinds = [new SerialPortsWidget()];
        _kinds = kinds.ToDictionary(kind => kind.DefinitionId, StringComparer.Ordinal);

        RestoreWidgets();
    }

    /// <summary>Set when the user has removed the last widget; the process can exit then.</summary>
    public WaitHandle NoWidgetsLeft => _noWidgetsLeft.WaitHandle;

    public bool HasWidgets => !_widgets.IsEmpty;

    public void CreateWidget(WidgetContext widgetContext)
    {
        var widget = Track(widgetContext);
        Log.Info($"CreateWidget {widget.DefinitionId} {widget.Size}");

        Update(widget, NewSnapshot(), force: true);
    }

    public void DeleteWidget(string widgetId, string customState)
    {
        _widgets.TryRemove(widgetId, out _);
        Log.Info("DeleteWidget");

        SyncTimer();
        if (_widgets.IsEmpty)
        {
            _noWidgetsLeft.Set();
        }
    }

    public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        var widget = Track(actionInvokedArgs.WidgetContext);
        var verb = actionInvokedArgs.Verb;

        try
        {
            if (!_kinds.TryGetValue(widget.DefinitionId, out var kind))
            {
                return;
            }

            var snapshot = NewSnapshot();
            if (!kind.HandleAction(widget, verb, actionInvokedArgs.Data, snapshot))
            {
                return;
            }

            Update(widget, snapshot, force: true);

            // Feedback such as "Copied" is shown for a moment, then the card returns to normal.
            _ = Task.Delay(SerialPortsWidget.CopiedFeedback + TimeSpan.FromMilliseconds(100))
                .ContinueWith(_ => Update(widget, NewSnapshot(), force: false), TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            Log.Error($"Action '{verb}' failed", ex);
        }
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        var widget = Track(contextChangedArgs.WidgetContext);
        Log.Info($"ContextChanged {widget.DefinitionId} {widget.Size}");

        Update(widget, NewSnapshot(), force: true);
    }

    public void OnCustomizationRequested(WidgetCustomizationRequestedArgs customizationInvokedArgs)
    {
        Log.Info("CustomizationRequested");
    }

    public void Activate(WidgetContext widgetContext)
    {
        var widget = Track(widgetContext);
        widget.IsActive = true;
        Log.Info($"Activate {widget.DefinitionId} {widget.Size}");

        Update(widget, NewSnapshot(), force: true);
        SyncTimer();
    }

    public void Deactivate(string widgetId)
    {
        if (_widgets.TryGetValue(widgetId, out var widget))
        {
            widget.IsActive = false;
            Log.Info($"Deactivate {widget.DefinitionId}");
        }

        SyncTimer();
    }

    private Snapshot NewSnapshot() => new(_settings, DateTimeOffset.Now);

    // The host may call for a widget it created while this process was not running.
    private WidgetInstance Track(WidgetContext context)
    {
        var widget = _widgets.GetOrAdd(context.Id, id => new WidgetInstance(id, context.DefinitionId));
        widget.Size = context.Size;
        _noWidgetsLeft.Reset();
        return widget;
    }

    private void RestoreWidgets()
    {
        try
        {
            // The list is null, not empty, when no widget is pinned.
            foreach (var info in WidgetManager.GetDefault().GetWidgetInfos() ?? [])
            {
                Track(info.WidgetContext);
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
                var seconds = Math.Clamp(_settings.Load().RefreshSeconds, MinRefreshSeconds, MaxRefreshSeconds);
                var interval = TimeSpan.FromSeconds(seconds);
                _timer = new Timer(_ => RefreshActive(), null, interval, interval);
                Log.Info($"Refresh started ({seconds} s)");
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
        // Skip a tick when the previous one is still reading devices.
        if (Interlocked.Exchange(ref _refreshing, 1) == 1)
        {
            return;
        }

        try
        {
            var snapshot = NewSnapshot();
            foreach (var widget in _widgets.Values.Where(widget => widget.IsActive))
            {
                Update(widget, snapshot, force: false);
            }
        }
        finally
        {
            Volatile.Write(ref _refreshing, 0);
        }
    }

    private void Update(WidgetInstance widget, Snapshot snapshot, bool force)
    {
        if (!_kinds.TryGetValue(widget.DefinitionId, out var kind))
        {
            return;
        }

        try
        {
            lock (widget.Gate)
            {
                var content = kind.Render(widget, snapshot);
                if (!force && content == widget.LastSent)
                {
                    return;
                }

                var options = new WidgetUpdateRequestOptions(widget.Id)
                {
                    Template = content.Template,
                    Data = content.Data,
                    CustomState = string.Empty,
                };

                WidgetManager.GetDefault().UpdateWidget(options);
                widget.LastSent = content;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Update failed for {widget.DefinitionId}", ex);
        }
    }
}
