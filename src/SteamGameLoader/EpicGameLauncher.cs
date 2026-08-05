using System.Diagnostics;

namespace SteamGameLoader;

/// <summary>
/// Epic has no equivalent of Steam's "Running" registry flag, so detection
/// instead resolves the game's exact executable from its local install
/// manifest, then watches specifically for that process to appear and show a
/// window. If no manifest can be found at all, there's nothing reliable to
/// watch for - rather than risk sitting open indefinitely (the problem the
/// earlier, heuristic-based Steam launcher-handoff detection ran into), it
/// closes immediately and just leaves Epic to open on its own.
/// </summary>
internal sealed class EpicGameLauncher : IGameLauncher
{
    private enum State { WaitingForProcess, WaitingForWindow }

    private readonly string _appName;
    private readonly string? _exePath;

    private State _state = State.WaitingForProcess;
    private int? _pid;

    public string DisplayName { get; }

    public EpicGameLauncher(string appName)
    {
        _appName = appName;

        EpicHelper.ManifestInfo? manifest = EpicHelper.TryGetManifest(appName);
        DisplayName = manifest?.DisplayName ?? $"Epic Games title ({appName})";
        _exePath = manifest?.ExecutablePath;
    }

    public void Start()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"com.epicgames.launcher://apps/{_appName}?action=launch&silent=true",
                UseShellExecute = true,
            });
        }
        catch
        {
            // If Epic itself can't be found/started there is nothing more we can
            // do here; the safety timeout will close the popup regardless.
        }
    }

    public bool HasLaunched()
    {
        if (_exePath is null)
            return true; // No manifest to pin down the real exe - fail open rather than risk getting stuck.

        if (_state == State.WaitingForProcess)
        {
            int? pid = ProcessLookup.FindPidForExecutable(_exePath);
            if (pid is null)
                return false;

            _pid = pid;
            _state = State.WaitingForWindow;
            return false;
        }

        if (_pid is not int trackedPid)
            return true;

        return !ProcessLookup.IsRunning(trackedPid) || ProcessLookup.HasVisibleWindow(trackedPid);
    }
}
