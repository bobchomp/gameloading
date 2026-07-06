using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SteamGameLoader;

internal sealed class DetectedShortcut
{
    public required string LnkPath { get; init; }
    public required string DisplayName { get; init; }
    public required uint AppId { get; init; }
    public required bool AlreadyConverted { get; init; }

    public override string ToString() =>
        AlreadyConverted
            ? $"{DisplayName}  →  AppID {AppId}  [already set up]"
            : $"{DisplayName}  →  AppID {AppId}";
}

/// <summary>
/// Finds existing Steam game shortcuts on the desktop, i.e. ones whose target
/// or arguments reference "steam://rungameid/&lt;id&gt;" or "-applaunch &lt;id&gt;",
/// which is what Steam itself creates when you use "Add desktop shortcut".
/// </summary>
internal static class ShortcutScanner
{
    private static readonly Regex AppIdRegex = new(@"(?:rungameid/|-applaunch\s+)(\d+)", RegexOptions.IgnoreCase);

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
                ShellLinkHelper.ShortcutInfo info;
                try
                {
                    info = ShellLinkHelper.Read(lnk);
                }
                catch
                {
                    continue;
                }

                bool alreadyConverted = string.Equals(
                    Path.GetFileName(info.TargetPath), exeFileName, StringComparison.OrdinalIgnoreCase);

                if (alreadyConverted && uint.TryParse(info.Arguments.Trim(), out uint convertedAppId))
                {
                    results.Add(new DetectedShortcut
                    {
                        LnkPath = lnk,
                        DisplayName = Path.GetFileNameWithoutExtension(lnk),
                        AppId = convertedAppId,
                        AlreadyConverted = true,
                    });
                    continue;
                }

                string combined = $"{info.TargetPath} {info.Arguments}";
                Match match = AppIdRegex.Match(combined);
                if (!match.Success)
                    continue;

                results.Add(new DetectedShortcut
                {
                    LnkPath = lnk,
                    DisplayName = Path.GetFileNameWithoutExtension(lnk),
                    AppId = uint.Parse(match.Groups[1].Value),
                    AlreadyConverted = false,
                });
            }
        }

        return results;
    }

    private static IEnumerable<string> GetDesktopFolders()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
    }
}
