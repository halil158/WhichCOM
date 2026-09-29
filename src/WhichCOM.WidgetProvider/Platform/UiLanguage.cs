using System.Runtime.InteropServices;
using WhichCOM.Core.Cards;

namespace WhichCOM.WidgetProvider.Platform;

internal static partial class UiLanguage
{
    private const string Automatic = "auto";
    private const int PrimaryLanguageMask = 0x3FF;
    private const int TurkishLanguage = 0x1F;

    /// <summary>
    /// Language for the cards. "auto" uses the display language of Windows; the culture of the
    /// process cannot be used because a packaged process only gets the languages of its manifest.
    /// </summary>
    public static string Resolve(string? setting)
    {
        if (!string.IsNullOrWhiteSpace(setting)
            && !string.Equals(setting.Trim(), Automatic, StringComparison.OrdinalIgnoreCase))
        {
            return setting.Trim();
        }

        var primary = GetUserDefaultUILanguage() & PrimaryLanguageMask;
        return primary == TurkishLanguage ? CardStrings.Turkish : CardStrings.English;
    }

    [LibraryImport("kernel32.dll")]
    private static partial ushort GetUserDefaultUILanguage();
}
