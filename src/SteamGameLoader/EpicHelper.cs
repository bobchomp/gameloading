using System;
using System.IO;
using System.Text.Json;

namespace SteamGameLoader;

/// <summary>
/// Epic Games Launcher has no Steam-like "Running" flag to poll, so instead
/// this reads its local install manifests (.item JSON files, one per
/// installed game) to find the game's exact executable path, which
/// EpicGameLauncher then watches for directly.
/// </summary>
internal static class EpicHelper
{
    public sealed class ManifestInfo
    {
        public required string DisplayName { get; init; }
        public required string ExecutablePath { get; init; }
    }

    /// <summary>
    /// Looks up a game's manifest by its Epic "AppName" catalog identifier -
    /// the same identifier that appears in its launch shortcut
    /// (com.epicgames.launcher://apps/&lt;AppName&gt;?action=launch&amp;silent=true).
    /// </summary>
    public static ManifestInfo? TryGetManifest(string appName)
    {
        try
        {
            string manifestsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests");

            if (!Directory.Exists(manifestsDir))
                return null;

            foreach (string file in Directory.EnumerateFiles(manifestsDir, "*.item"))
            {
                ManifestInfo? info = TryReadManifest(file, appName);
                if (info is not null)
                    return info;
            }
        }
        catch
        {
            // Best effort only.
        }

        return null;
    }

    private static ManifestInfo? TryReadManifest(string manifestPath, string appName)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("AppName", out JsonElement appNameEl))
                return null;

            string? manifestAppName = appNameEl.GetString();
            bool matches = string.Equals(manifestAppName, appName, StringComparison.OrdinalIgnoreCase);

            // Some shortcuts embed the extended "Namespace:ItemId:AppName" form -
            // fall back to matching just the last segment against it.
            if (!matches)
            {
                string lastSegment = appName.Contains(':') ? appName[(appName.LastIndexOf(':') + 1)..] : appName;
                matches = string.Equals(manifestAppName, lastSegment, StringComparison.OrdinalIgnoreCase);
            }

            if (!matches)
                return null;

            if (!root.TryGetProperty("InstallLocation", out JsonElement installEl) ||
                !root.TryGetProperty("LaunchExecutable", out JsonElement exeEl))
                return null;

            string? installLocation = installEl.GetString();
            string? launchExecutable = exeEl.GetString();
            if (string.IsNullOrEmpty(installLocation) || string.IsNullOrEmpty(launchExecutable))
                return null;

            string displayName = root.TryGetProperty("DisplayName", out JsonElement dn)
                ? dn.GetString() ?? appName
                : appName;

            return new ManifestInfo
            {
                DisplayName = displayName,
                ExecutablePath = Path.Combine(installLocation, launchExecutable),
            };
        }
        catch
        {
            return null; // Skip unreadable/malformed manifest, caller tries the next one.
        }
    }

    /// <summary>Best-effort path to the Epic Games Launcher itself, used as an icon fallback.</summary>
    public static string? TryGetLauncherPath()
    {
        foreach (Environment.SpecialFolder folder in new[]
                 {
                     Environment.SpecialFolder.ProgramFilesX86,
                     Environment.SpecialFolder.ProgramFiles,
                 })
        {
            string candidate = Path.Combine(
                Environment.GetFolderPath(folder), "Epic Games", "Launcher", "Portal", "Binaries", "Win32", "EpicGamesLauncher.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
