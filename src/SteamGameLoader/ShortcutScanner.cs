using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SteamGameLoader;

internal sealed class DetectedShortcut
{
    public required string ShortcutPath { get; init; }
    public required string DisplayName { get; init; }
    public required uint AppId { get; init; }
    public required bool AlreadyConverted { get; init; }
    public required bool IsUrlShortcut { get; init; }
    public string? UrlIconFile { get; init; }
    public int UrlIconIndex { get; init; }

    public override string ToString() =>
        AlreadyConverted
            ? $"{DisplayName}  →  AppID {AppId}  [already set up]"
            : $"{DisplayName}  →  AppID {AppId}";
}

/// <summary>
/// Finds existing Steam game shortcuts on the desktop. Steam's own "Add
/// desktop shortcut" feature can create either:
///  - a ".url" Internet Shortcut with "URL=steam://rungameid/&lt;id&gt;", or
///  - a ".lnk" pointing at steam.exe with "-applaunch &lt;id&gt;", or
///  - a ".lnk" pointing straight at the game's .exe with no id anywhere in it
///    (e.g. a shortcut dragged/created from the install folder), in which
///    case the app id is recovered from Steam's appmanifest install dir.
/// </summary>
internal static class ShortcutScanner
{
    private static readonly Regex AppIdRegex = new(@"(?:rungameid/|-applaunch\s+)(\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex UrlRegex = new(@"URL\s*=\s*steam://rungameid/(\d+)", RegexOptions.IgnoreCase);
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

        if (alreadyConverted && uint.TryParse(info.Arguments.Trim(), out uint convertedAppId))
        {
            return new DetectedShortcut
            {
                ShortcutPath = lnkPath,
                DisplayName = Path.GetFileNameWithoutExtension(lnkPath),
                AppId = convertedAppId,
                AlreadyConverted = true,
                IsUrlShortcut = false,
            };
        }

        string combined = $"{info.TargetPath} {info.Arguments}";
        Match match = AppIdRegex.Match(combined);
        uint? appId = match.Success ? uint.Parse(match.Groups[1].Value) : SteamHelper.TryResolveAppIdFromExePath(info.TargetPath);

        if (appId is null)
            return null;

        return new DetectedShortcut
        {
            ShortcutPath = lnkPath,
            DisplayName = Path.GetFileNameWithoutExtension(lnkPath),
            AppId = appId.Value,
            AlreadyConverted = false,
            IsUrlShortcut = false,
        };
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

        Match match = UrlRegex.Match(content);
        if (!match.Success)
            return null;

        Match iconFileMatch = UrlIconFileRegex.Match(content);
        Match iconIndexMatch = UrlIconIndexRegex.Match(content);

        return new DetectedShortcut
        {
            ShortcutPath = urlPath,
            DisplayName = Path.GetFileNameWithoutExtension(urlPath),
            AppId = uint.Parse(match.Groups[1].Value),
            AlreadyConverted = false,
            IsUrlShortcut = true,
            UrlIconFile = iconFileMatch.Success ? iconFileMatch.Groups[1].Value.Trim() : null,
            UrlIconIndex = iconIndexMatch.Success ? int.Parse(iconIndexMatch.Groups[1].Value) : 0,
        };
    }

    private static IEnumerable<string> GetDesktopFolders()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
    }
}
