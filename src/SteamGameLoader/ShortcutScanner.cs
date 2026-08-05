using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SteamGameLoader;

internal enum GamePlatform { Steam, Epic }

internal sealed class DetectedShortcut
{
    public required string ShortcutPath { get; init; }
    public required string DisplayName { get; init; }
    public required GamePlatform Platform { get; init; }

    /// <summary>Steam: numeric App ID as a string. Epic: the "AppName" catalog identifier.</summary>
    public required string GameId { get; init; }

    public required bool AlreadyConverted { get; init; }
    public required bool IsUrlShortcut { get; init; }
    public string? UrlIconFile { get; init; }
    public int UrlIconIndex { get; init; }

    /// <summary>The argument this app should be launched with once converted, e.g. "steam:730" or "epic:Farmer".</summary>
    public string LauncherArgument => $"{(Platform == GamePlatform.Steam ? "steam" : "epic")}:{GameId}";

    public override string ToString()
    {
        string platformLabel = Platform == GamePlatform.Steam ? "Steam" : "Epic";
        return AlreadyConverted
            ? $"{DisplayName}  →  {platformLabel} {GameId}  [already set up]"
            : $"{DisplayName}  →  {platformLabel} {GameId}";
    }
}

/// <summary>
/// Finds existing Steam and Epic Games game shortcuts on the desktop.
///
/// Steam's own "Add desktop shortcut" feature can create either:
///  - a ".url" Internet Shortcut with "URL=steam://rungameid/&lt;id&gt;", or
///  - a ".lnk" pointing at steam.exe with "-applaunch &lt;id&gt;", or
///  - a ".lnk" pointing straight at the game's .exe with no id anywhere in it
///    (e.g. a shortcut dragged/created from the install folder), in which
///    case the app id is recovered from Steam's appmanifest install dir.
///
/// Epic Games Launcher's "Create Shortcut" creates a ".url" (or occasionally
/// ".lnk") shortcut whose target is
/// "com.epicgames.launcher://apps/&lt;AppName&gt;?action=launch&amp;silent=true".
/// </summary>
internal static class ShortcutScanner
{
    private static readonly Regex SteamAppIdRegex = new(@"(?:rungameid/|-applaunch\s+)(\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex SteamUrlRegex = new(@"URL\s*=\s*steam://rungameid/(\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex EpicIdRegex = new(@"com\.epicgames\.launcher://apps/([^?&\s""]+)", RegexOptions.IgnoreCase);
    private static readonly Regex UrlIconFileRegex = new(@"^IconFile\s*=\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex UrlIconIndexRegex = new(@"^IconIndex\s*=\s*(-?\d+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

    public static List<DetectedShortcut> ScanDesktopShortcuts()
    {
        var results = new List<DetectedShortcut>();
        string exeFileName = Path.GetFileName(Application.ExecutablePath);

        foreach (string desktop in GetDesktopFolders())
        {
            if (!Directory.Exists(desktop))
                continue;

            foreach (string lnk in Directory.EnumerateFiles(desktop, "*.lnk"))
            {
                DetectedShortcut? shortcut = TryReadLnkShortcut(lnk, exeFileName);
                if (shortcut is not null)
                    results.Add(shortcut);
            }

            foreach (string url in Directory.EnumerateFiles(desktop, "*.url"))
            {
                DetectedShortcut? shortcut = TryReadUrlShortcut(url);
                if (shortcut is not null)
                    results.Add(shortcut);
            }
        }

        return results;
    }

    private static DetectedShortcut? TryReadLnkShortcut(string lnkPath, string exeFileName)
    {
        ShellLinkHelper.ShortcutInfo info;
        try
        {
            info = ShellLinkHelper.Read(lnkPath);
        }
        catch
        {
            return null;
        }

        bool alreadyConverted = string.Equals(
            Path.GetFileName(info.TargetPath), exeFileName, StringComparison.OrdinalIgnoreCase);

        if (alreadyConverted && TryParseLauncherArgument(info.Arguments, out GamePlatform convertedPlatform, out string convertedId))
        {
            return new DetectedShortcut
            {
                ShortcutPath = lnkPath,
                DisplayName = Path.GetFileNameWithoutExtension(lnkPath),
                Platform = convertedPlatform,
                GameId = convertedId,
                AlreadyConverted = true,
                IsUrlShortcut = false,
            };
        }

        string combined = $"{info.TargetPath} {info.Arguments}";

        Match steamMatch = SteamAppIdRegex.Match(combined);
        if (steamMatch.Success)
            return BuildShortcut(lnkPath, GamePlatform.Steam, steamMatch.Groups[1].Value, isUrl: false);

        Match epicMatch = EpicIdRegex.Match(combined);
        if (epicMatch.Success)
            return BuildShortcut(lnkPath, GamePlatform.Epic, epicMatch.Groups[1].Value, isUrl: false);

        uint? resolvedSteamAppId = SteamHelper.TryResolveAppIdFromExePath(info.TargetPath);
        if (resolvedSteamAppId is uint appId)
            return BuildShortcut(lnkPath, GamePlatform.Steam, appId.ToString(), isUrl: false);

        return null;
    }

    private static DetectedShortcut? TryReadUrlShortcut(string urlPath)
    {
        string content;
        try
        {
            content = File.ReadAllText(urlPath);
        }
        catch
        {
            return null;
        }

        Match steamMatch = SteamUrlRegex.Match(content);
        if (steamMatch.Success)
            return BuildShortcut(urlPath, GamePlatform.Steam, steamMatch.Groups[1].Value, isUrl: true, content);

        Match epicMatch = EpicIdRegex.Match(content);
        if (epicMatch.Success)
            return BuildShortcut(urlPath, GamePlatform.Epic, epicMatch.Groups[1].Value, isUrl: true, content);

        return null;
    }

    private static DetectedShortcut BuildShortcut(
        string shortcutPath, GamePlatform platform, string gameId, bool isUrl, string? urlContent = null)
    {
        string? iconFile = null;
        int iconIndex = 0;

        if (isUrl && urlContent is not null)
        {
            Match iconFileMatch = UrlIconFileRegex.Match(urlContent);
            Match iconIndexMatch = UrlIconIndexRegex.Match(urlContent);
            iconFile = iconFileMatch.Success ? iconFileMatch.Groups[1].Value.Trim() : null;
            iconIndex = iconIndexMatch.Success ? int.Parse(iconIndexMatch.Groups[1].Value) : 0;
        }

        return new DetectedShortcut
        {
            ShortcutPath = shortcutPath,
            DisplayName = Path.GetFileNameWithoutExtension(shortcutPath),
            Platform = platform,
            GameId = gameId,
            AlreadyConverted = false,
            IsUrlShortcut = isUrl,
            UrlIconFile = iconFile,
            UrlIconIndex = iconIndex,
        };
    }

    /// <summary>
    /// Parses a converted shortcut's Arguments: "steam:&lt;id&gt;" or "epic:&lt;id&gt;",
    /// or a bare number for shortcuts converted by earlier versions of this app.
    /// </summary>
    private static bool TryParseLauncherArgument(string arguments, out GamePlatform platform, out string gameId)
    {
        arguments = arguments.Trim();
        int colonIndex = arguments.IndexOf(':');

        if (colonIndex < 0)
        {
            if (uint.TryParse(arguments, out _))
            {
                platform = GamePlatform.Steam;
                gameId = arguments;
                return true;
            }

            platform = default;
            gameId = "";
            return false;
        }

        string platformPart = arguments[..colonIndex];
        string idPart = arguments[(colonIndex + 1)..];

        if (idPart.Length > 0)
        {
            switch (platformPart.ToLowerInvariant())
            {
                case "steam":
                    platform = GamePlatform.Steam;
                    gameId = idPart;
                    return true;
                case "epic":
                    platform = GamePlatform.Epic;
                    gameId = idPart;
                    return true;
            }
        }

        platform = default;
        gameId = "";
        return false;
    }

    private static IEnumerable<string> GetDesktopFolders()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
    }
}
