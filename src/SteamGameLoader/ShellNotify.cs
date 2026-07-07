using System;
using System.Runtime.InteropServices;

namespace SteamGameLoader;

/// <summary>
/// Explorer's Desktop view doesn't always notice files added/removed by an
/// external process and can keep showing a stale "ghost" icon (which then
/// errors with "Item Not Found" when clicked). Broadcasting a shell change
/// notification after we touch shortcut files forces an immediate refresh.
/// </summary>
internal static class ShellNotify
{
    private const long ShcneAssocChanged = 0x08000000;
    private const uint ShcnfIdList = 0x0000;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(long wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    public static void RefreshDesktopIcons() =>
        SHChangeNotify(ShcneAssocChanged, ShcnfIdList, IntPtr.Zero, IntPtr.Zero);
}
