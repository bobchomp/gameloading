using System;
using System.Windows.Forms;

namespace SteamGameLoader;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Shortcuts created by the setup tool invoke us as: SteamGameLoader.exe <appid>
        if (args.Length >= 1 && uint.TryParse(args[0], out uint appId))
        {
            Application.Run(new LoadingForm(appId));
        }
        else
        {
            // No app id supplied -> show the shortcut setup/converter tool instead.
            Application.Run(new SetupForm());
        }
    }
}
