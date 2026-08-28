using System;
using System.IO;

namespace SteamGameLoader;

/// <summary>
/// Remembers the last update version the user clicked "Later" on, so - since
/// the update check now runs on every game launch - clicking Later doesn't
/// mean seeing the exact same prompt again on the very next game you start.
/// A genuinely newer version still prompts again.
/// </summary>
internal static class UpdateDismissal
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteamGameLoader", "dismissed-update.txt");

    public static bool WasDismissed(string version)
    {
        try
        {
            return File.Exists(FilePath) && File.ReadAllText(FilePath).Trim() == version;
        }
        catch
        {
            return false;
        }
    }

    public static void Dismiss(string version)
    {
        try
        {
            string? dir = Path.GetDirectoryName(FilePath);
            if (dir is not null)
                Directory.CreateDirectory(dir);

            File.WriteAllText(FilePath, version);
        }
        catch
        {
            // Best effort only - worst case, the prompt just reappears next time.
        }
    }
}
