using System.Collections.Concurrent;
using System.Reflection;

namespace WhichCOM.WidgetProvider;

/// <summary>Adaptive Card templates, embedded in the assembly from the Templates folder.</summary>
internal static class TemplateStore
{
    private const string Prefix = "WhichCOM.WidgetProvider.Templates.";

    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    public static string Get(string name) => Cache.GetOrAdd(name, Load);

    private static string Load(string name)
    {
        var resource = $"{Prefix}{name}.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Template '{resource}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
