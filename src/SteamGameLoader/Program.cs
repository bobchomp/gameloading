using System.Threading;
using System.Windows.Forms;

namespace SteamGameLoader;

internal static class Program
{
    /// <summary>
    /// Referenced by installer/setup.iss's AppMutex, so Inno Setup can detect a
    /// running instance and prompt to close it - a backstop for the self-update
    /// flow (UpdateAvailableForm) alongside its own explicit Environment.Exit.
    /// </summary>
    private const string AppMutexName = "SteamGameLoaderAppMutex";

    [STAThread]
    private static void Main(string[] args)
    {
        using var appMutex = new Mutex(initiallyOwned: false, name: AppMutexName);

        ApplicationConfiguration.Initialize();

        // Shortcuts created by the setup tool invoke us as:
        //   SteamGameLoader.exe steam:<appid>
        //   SteamGameLoader.exe epic:<appName>
        // A bare number (no "platform:" prefix) is a Steam App ID, kept for
        // shortcuts converted by earlier versions of this app.
        IGameLauncher? launcher = args.Length >= 1 ? TryParseLauncher(args[0]) : null;
        if (launcher is not null)
        {
            var loadingForm = new LoadingForm(launcher);
            Application.Run(loadingForm);

            // Checked in the background while the popup was up - only shown if
            // it happened to finish in time, never something the game launch
            // waits on.
            if (loadingForm.PendingUpdate is UpdateChecker.UpdateInfo update)
            {
                Application.Run(new UpdateAvailableForm(update));
            }
        }
        else
        {
            // No (valid) launcher argument -> show the shortcut setup/converter tool instead.
            Application.Run(new SetupForm());
        }
    }

    private static IGameLauncher? TryParseLauncher(string arg)
    {
        int colonIndex = arg.IndexOf(':');
        if (colonIndex < 0)
        {
            return uint.TryParse(arg, out uint legacyAppId) ? new SteamGameLauncher(legacyAppId) : null;
        }

        string platform = arg[..colonIndex];
        string id = arg[(colonIndex + 1)..];
        if (id.Length == 0)
            return null;

        return platform.ToLowerInvariant() switch
        {
            "steam" => uint.TryParse(id, out uint appId) ? new SteamGameLauncher(appId) : null,
            "epic" => new EpicGameLauncher(id),
            _ => null,
        };
    }
}
