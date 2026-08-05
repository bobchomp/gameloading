using System.Windows.Forms;

namespace SteamGameLoader;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Shortcuts created by the setup tool invoke us as:
        //   SteamGameLoader.exe steam:<appid>
        //   SteamGameLoader.exe epic:<appName>
        // A bare number (no "platform:" prefix) is a Steam App ID, kept for
        // shortcuts converted by earlier versions of this app.
        IGameLauncher? launcher = args.Length >= 1 ? TryParseLauncher(args[0]) : null;
        if (launcher is not null)
        {
            Application.Run(new LoadingForm(launcher));
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
