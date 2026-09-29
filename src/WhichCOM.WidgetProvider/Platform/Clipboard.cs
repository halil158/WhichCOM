using System.Runtime.InteropServices;

namespace WhichCOM.WidgetProvider.Platform;

/// <summary>
/// Puts text on the clipboard. The provider has no window and is called on COM worker threads,
/// so the work is done on a short-lived STA thread with a message-only window as clipboard owner.
/// </summary>
internal static partial class Clipboard
{
    private const uint UnicodeText = 13;
    private const uint Moveable = 0x0002;
    private const int OpenAttempts = 10;

    private static readonly IntPtr MessageOnlyParent = new(-3);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public static bool SetText(string text)
    {
        var succeeded = false;

        var thread = new Thread(() => succeeded = SetTextOnThisThread(text)) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return thread.Join(Timeout) && succeeded;
    }

    private static unsafe bool SetTextOnThisThread(string text)
    {
        var owner = CreateWindowEx(0, "STATIC", string.Empty, 0, 0, 0, 0, 0, MessageOnlyParent, 0, 0, 0);

        try
        {
            if (!TryOpen(owner))
            {
                return false;
            }

            try
            {
                EmptyClipboard();

                var bytes = (nuint)((text.Length + 1) * sizeof(char));
                var memory = GlobalAlloc(Moveable, bytes);
                if (memory == 0)
                {
                    return false;
                }

                var target = (char*)GlobalLock(memory);
                if (target is null)
                {
                    GlobalFree(memory);
                    return false;
                }

                text.AsSpan().CopyTo(new Span<char>(target, text.Length));
                target[text.Length] = '\0';
                GlobalUnlock(memory);

                // On success the clipboard owns the memory.
                if (SetClipboardData(UnicodeText, memory) == 0)
                {
                    GlobalFree(memory);
                    return false;
                }

                return true;
            }
            finally
            {
                CloseClipboard();
            }
        }
        finally
        {
            if (owner != 0)
            {
                DestroyWindow(owner);
            }
        }
    }

    // Another application may have the clipboard open for a moment.
    private static bool TryOpen(IntPtr owner)
    {
        for (var attempt = 0; attempt < OpenAttempts; attempt++)
        {
            if (OpenClipboard(owner))
            {
                return true;
            }

            Thread.Sleep(20);
        }

        return false;
    }

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(IntPtr window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(IntPtr owner);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll")]
    private static partial IntPtr SetClipboardData(uint format, IntPtr memory);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll")]
    private static unsafe partial void* GlobalLock(IntPtr memory);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(IntPtr memory);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GlobalFree(IntPtr memory);
}
