using WhichCOM.WidgetProvider.Com;

namespace WhichCOM.WidgetProvider;

internal static class Program
{
    /// <summary>Must match the class ID in Package.appxmanifest (two places).</summary>
    public static readonly Guid ProviderClassId = new("8F416A1C-B8FB-4DCB-978E-CCA7E588E858");

    // COM passes this argument when it starts the process for the widget host.
    private const string ComActivationArgument = "-Embedding";

    private static readonly TimeSpan IdleCheckInterval = TimeSpan.FromMinutes(1);

    [MTAThread]
    private static int Main(string[] args)
    {
        if (!args.Contains(ComActivationArgument, StringComparer.OrdinalIgnoreCase))
        {
            // Started by hand. There is nothing to show; the widgets live on the widget board.
            return 0;
        }

        try
        {
            Log.Info("Provider starting");

            var provider = new WidgetProvider();
            var cookie = ComServer.Register(ProviderClassId, new WidgetProviderFactory(provider));

            // Stay alive while widgets are pinned. Also leave when the process was started but
            // the host never created a widget.
            while (!provider.NoWidgetsLeft.WaitOne(IdleCheckInterval) && provider.HasWidgets)
            {
            }

            ComServer.Revoke(cookie);
            Log.Info("Provider exiting: no widgets left");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("Provider failed", ex);
            return 1;
        }
    }
}
