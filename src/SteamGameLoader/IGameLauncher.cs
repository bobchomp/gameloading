namespace SteamGameLoader;

/// <summary>
/// Abstracts "start this game, then tell me once it's actually launched" so
/// LoadingForm doesn't need to know which storefront a shortcut came from.
/// </summary>
internal interface IGameLauncher
{
    /// <summary>Shown as the popup's subtitle.</summary>
    string DisplayName { get; }

    /// <summary>Kicks off the actual launch (e.g. via a storefront's launch URI).</summary>
    void Start();

    /// <summary>
    /// Polled every 500ms. Returns true once the game should be considered
    /// launched, at which point the popup closes.
    /// </summary>
    bool HasLaunched();
}
