using System.Globalization;
using WhichCOM.Core.Settings;

namespace WhichCOM.WidgetProvider;

/// <summary>
/// Small diagnostic log in the settings directory. The provider has no window and is started by
/// the widget host, so this file is the first place to look when a widget does not show up.
/// </summary>
internal static class Log
{
    private const string FileName = "provider.log";
    private const long MaxBytes = 256 * 1024;

    private static readonly Lock Gate = new();
    private static readonly string FilePath = Path.Combine(SettingsStore.DefaultDirectory(), FileName);

    public static void Info(string message) => Write("INF", message);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERR", exception is null ? message : $"{message}: {exception}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                {
                    file.MoveTo(FilePath + ".old", overwrite: true);
                }

                var time = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                File.AppendAllText(FilePath, $"{time} {level} {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never break the provider.
        }
    }
}
