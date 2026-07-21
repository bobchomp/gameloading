using System;
using System.IO;

namespace SteamGameLoader;

/// <summary>
/// Pins a shortcut to the taskbar via Explorer's "Pin to taskbar" shell verb,
/// invoked through Shell.Application COM automation. Microsoft removed the
/// supported API for this in Windows 10 (~2018) specifically to stop apps
/// from pinning things without a user's own click, and has continued to
/// quietly break the old unsupported verb across updates - this works on
/// many current systems, but isn't guaranteed and can stop working after a
/// future Windows update with no warning.
/// </summary>
internal static class TaskbarPinHelper
{
    public static bool TryPin(string shortcutPath)
    {
        try
        {
            string? directory = Path.GetDirectoryName(shortcutPath);
            string fileName = Path.GetFileName(shortcutPath);
            if (string.IsNullOrEmpty(directory))
                return false;

            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null)
                return false;

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell is null)
                return false;

            dynamic? folder = shell.Namespace(directory);
            dynamic? item = folder?.ParseName(fileName);
            if (item is null)
                return false;

            foreach (dynamic verb in item.Verbs())
            {
                string verbName = ((string)verb.Name).Replace("&", "");
                if (verbName.Contains("taskbar", StringComparison.OrdinalIgnoreCase))
                {
                    verb.DoIt();
                    return true;
                }
            }
        }
        catch
        {
            // Best effort only - see class remarks.
        }

        return false;
    }
}
