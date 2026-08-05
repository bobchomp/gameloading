using System.Diagnostics;

namespace SteamGameLoader;

internal sealed class SteamGameLauncher : IGameLauncher
{
    private readonly uint _appId;

    public string DisplayName { get; }

    public SteamGameLauncher(uint appId)
    {
        _appId = appId;
        DisplayName = SteamHelper.TryGetGameName(appId) ?? $"Steam game (AppID {appId})";
    }

    public void Start()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"steam://rungameid/{_appId}",
                UseShellExecute = true,
            });
        }
        catch
        {
            // If Steam itself can't be found/started there is nothing more we can
            // do here; the safety timeout will close the popup regardless.
        }
    }

    /// <summary>
    /// Steam writes HKCU\Software\Valve\Steam\Apps\&lt;appid&gt;\Running = 1 the
    /// moment it hands control to a game, and back to 0 when it exits. This is
    /// the same signal Steam itself uses, so it works for any app id without
    /// needing to know the game's actual executable name.
    /// </summary>
    public bool HasLaunched() => SteamHelper.IsGameRunning(_appId);
}
