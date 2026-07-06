using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SteamGameLoader;

/// <summary>
/// Reads Steam's own local state to know when a game has actually launched,
/// and to look up a human-readable game name for the loading popup.
/// </summary>
internal static class SteamHelper
{
    /// <summary>
    /// Steam writes HKCU\Software\Valve\Steam\Apps\&lt;appid&gt;\Running = 1 the
    /// moment it hands control to a game, and back to 0 when it exits. This is
    /// the same signal Steam itself uses, so it works for any app id without
    /// needing to know the game's actual executable name.
    /// </summary>
    public static bool IsGameRunning(uint appId)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey($@"Software\Valve\Steam\Apps\{appId}");
        return key?.GetValue("Running") is int running && running == 1;
    }

    public static string? TryGetGameName(uint appId)
    {
        try
        {
            string? steamPath = GetSteamInstallPath();
            if (steamPath is null)
                return null;

            foreach (string libraryFolder in GetLibraryFolders(steamPath))
            {
                string manifestPath = Path.Combine(libraryFolder, "steamapps", $"appmanifest_{appId}.acf");
                if (!File.Exists(manifestPath))
                    continue;

                string content = File.ReadAllText(manifestPath);
                Match match = Regex.Match(content, "\"name\"\\s*\"([^\"]*)\"");
                if (match.Success)
                    return match.Groups[1].Value;
            }
        }
        catch
        {
            // Best effort only - fall back to a generic label.
        }

        return null;
    }

    public static string? GetSteamInstallPath()
    {
        using RegistryKey? hkcu = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if (hkcu?.GetValue("SteamPath") is string p1 && p1.Length > 0)
        {
            string normalized = p1.Replace('/', '\\');
            if (Directory.Exists(normalized))
                return normalized;
        }

        using RegistryKey? hklm = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                                  ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
        if (hklm?.GetValue("InstallPath") is string p2 && Directory.Exists(p2))
            return p2;

        return null;
    }

    private static IEnumerable<string> GetLibraryFolders(string steamPath)
    {
        yield return steamPath;

        string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath))
            yield break;

        string content = File.ReadAllText(vdfPath);
        foreach (Match m in Regex.Matches(content, "\"path\"\\s*\"([^\"]*)\""))
        {
            string path = m.Groups[1].Value.Replace(@"\\", @"\");
            if (Directory.Exists(path))
                yield return path;
        }
    }
}
