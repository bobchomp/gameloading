using System.Windows.Forms;

namespace SteamGameLoader;

/// <summary>
/// A Form's title-bar/taskbar icon doesn't automatically follow the exe's
/// ApplicationIcon - it stays the generic default unless Form.Icon is set
/// explicitly. Extracting it from our own running exe keeps every window
/// in sync with whatever icon the exe was built with.
/// </summary>
internal static class AppIcon
{
    public static System.Drawing.Icon? TryLoad() =>
        System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
}
