using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteamGameLoader;

/// <summary>
/// Checks GitHub's public releases API for a newer version than the one
/// currently running, and can download + launch that release's installer.
/// Every method here fails silently (returns null/false) on any error -
/// a broken update check should never surface as an error to the user, it
/// should just quietly not find an update this time.
/// </summary>
internal static class UpdateChecker
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/bobchomp/gameloading/releases/latest";

    public sealed class UpdateInfo
    {
        public required string Version { get; init; }
        public required string DownloadUrl { get; init; }
        public required string FileName { get; init; }
    }

    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            string? currentVersion = GetCurrentVersion();
            if (currentVersion is null)
                return null;

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SteamGameLoader-UpdateCheck");

            using HttpResponseMessage response = await http.GetAsync(LatestReleaseUrl);
            if (!response.IsSuccessStatusCode)
                return null;

            using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out JsonElement tagEl))
                return null;

            string latestVersion = (tagEl.GetString() ?? "").TrimStart('v', 'V');
            if (latestVersion.Length == 0 || !IsNewer(latestVersion, currentVersion))
                return null;

            if (!root.TryGetProperty("assets", out JsonElement assetsEl))
                return null;

            foreach (JsonElement asset in assetsEl.EnumerateArray())
            {
                string? name = asset.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
                string? url = asset.TryGetProperty("browser_download_url", out JsonElement urlEl) ? urlEl.GetString() : null;

                if (name is not null && url is not null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return new UpdateInfo { Version = latestVersion, DownloadUrl = url, FileName = name };
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<bool> DownloadAndLaunchInstallerAsync(UpdateInfo update)
    {
        try
        {
            string tempPath = Path.Combine(Path.GetTempPath(), update.FileName);

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) })
            {
                byte[] data = await http.GetByteArrayAsync(update.DownloadUrl);
                await File.WriteAllBytesAsync(tempPath, data);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true,
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetCurrentVersion()
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(Application.ExecutablePath).ProductVersion;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsNewer(string latest, string current)
    {
        // Compare only the numeric dotted core (e.g. "1.2.3" out of "1.2.3-dev"),
        // since our dev/test builds carry a non-numeric suffix.
        string latestCore = latest.Split('-')[0];
        string currentCore = current.Split('-')[0];

        if (Version.TryParse(latestCore, out Version? latestV) && Version.TryParse(currentCore, out Version? currentV))
            return latestV > currentV;

        return !string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);
    }
}
