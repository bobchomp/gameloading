# Steam Loading Popups

Shows a "Game Loading…" popup with a spinner the moment you launch a game from
its Steam (or Epic Games) desktop shortcut, and closes itself automatically as
soon as the game has actually started — covering the gap between
double-clicking the shortcut and the game window appearing.

## How it works

1. **Setup (one-time):** run the app with no arguments (Start Menu shortcut
   "Steam Loading Popups", installed alongside it) to open the setup tool. It
   scans your Desktop for existing Steam and Epic Games shortcuts and lets you
   convert the ones you want:
   - **Steam** — shortcuts made via *right-click a game → Manage → Add
     desktop shortcut*: a `.url` Internet Shortcut (`steam://rungameid/<id>`),
     a `.lnk` pointing at `steam.exe -applaunch <id>`, or a `.lnk` pointing
     straight at the game's `.exe` (the App ID is then recovered by matching
     the exe's folder against Steam's own `appmanifest_*.acf` files).
   - **Epic Games** — shortcuts made via *right-click a game in your Library →
     Create Shortcut*: a `.url` or `.lnk` whose target is
     `com.epicgames.launcher://apps/<AppName>?action=launch&silent=true`.

   Converting a shortcut rewrites it to point at this app with the game's ID
   instead, while keeping the same name and icon (a `.url` shortcut becomes a
   `.lnk`, since only `.lnk` files can pass arguments to a program). The
   original is backed up so you can restore it later from the same setup
   tool.
2. **Launch:** double-click a converted shortcut. The app immediately starts
   the game through its storefront's own launch URI and shows the loading
   popup.
3. **Detection differs by storefront:**
   - **Steam** sets `HKCU\Software\Valve\Steam\Apps\<appid>\Running = 1` the
     instant it hands off to the game (and back to `0` when it exits). The
     popup polls that flag twice a second and closes as soon as it flips to
     `1` — this works for any game without needing to know its actual `.exe`
     name, and also covers cold starts where Steam itself has to launch
     first.
   - **Epic Games** has no equivalent flag, so instead the popup reads the
     game's own local install manifest (`.item` file under
     `C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests`) to find its exact
     executable, then watches specifically for that process to appear and
     show a window. If no manifest can be found for a game, there's nothing
     reliable to watch for, so the popup closes immediately rather than risk
     sitting open indefinitely.
4. **Safety net:** the popup also has a Cancel button and auto-closes after
   90 seconds regardless, so it can never get stuck open if detection doesn't
   fire (e.g. Steam/Epic is offline, or the id is wrong).
5. **Pin to Taskbar (optional):** check an already-converted shortcut in the
   setup tool and click "Pin to Taskbar" to pin it straight to the taskbar,
   same as pinning any other program - it launches through the loading popup
   just like the Desktop shortcut. This uses an old, unsupported Explorer
   shell verb, so it may not work on every Windows version; if it doesn't,
   right-click the converted Desktop shortcut yourself and choose "Pin to
   taskbar" (under "Show more options" on Windows 11) - that always works,
   since it's just a normal shortcut pointing at this app.
6. **Auto-update:** both the loading popup and the setup tool check GitHub's
   latest release in the background (never blocking a game launch - if the
   check hasn't finished by the time the popup would otherwise close, it just
   doesn't show a prompt that time). If a newer version is found, a small
   "Update available" popup offers **Update Now**, which downloads that
   release's installer, launches it, and closes this app so the installer can
   overwrite it - or **Later**, which remembers not to prompt again for that
   same version (a genuinely newer release will still prompt).

## Installing

Download the latest `SteamGameLoadingPopup-Setup-<version>.exe` from the
[Releases](../../releases) page and run it. At the end of setup you'll be
offered the chance to open the shortcut setup tool right away.

## Building locally

Requires the .NET 8 SDK and (optionally) [Inno Setup 6](https://jrsoftware.org/isinfo.php)
to build the installer.

```powershell
# Build the self-contained exe
dotnet publish src/SteamGameLoader/SteamGameLoader.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish

# Build the installer (requires Inno Setup's ISCC.exe on PATH)
iscc "/DMyAppVersion=1.0.0" installer\setup.iss
```

## Releasing via GitHub Actions

Push a tag like `v1.0.0` and the `Build and Release` workflow
(`.github/workflows/release.yml`) will build the app, compile the Inno Setup
installer, and attach `SteamGameLoadingPopup-Setup-<version>.exe` (e.g.
`SteamGameLoadingPopup-Setup-1.0.0.exe`) to a GitHub Release for that tag
automatically (creating the release if it doesn't exist yet, or updating it
- e.g. adding the asset - if it does).

You can also trigger it manually from the Actions tab (`workflow_dispatch`)
and type a version number (e.g. `1.0.0`) into the "Run workflow" box - this
does exactly the same create-or-update against a release with that tag name,
which is handy if you already created the release by hand from the GitHub UI.
Leaving the version box at its default `0.0.0-dev` just builds and uploads a
throwaway build artifact, without touching any release - useful for
verifying the app still compiles without publishing anything.

## Limitations

- Windows only, and only wraps *existing* Steam/Epic desktop shortcuts — it
  does not intercept launches from inside the Steam or Epic Games client
  itself.
- For Steam games that hand off to a separate launcher or anti-cheat process
  before the actual game starts, Steam's `Running` flag (and so the popup)
  may close slightly before the game's own window appears.
- Epic detection depends on the game having a readable local manifest file.
  If Epic's manifest can't be found or is missing the expected fields, the
  popup closes immediately instead of waiting, since there's no reliable
  signal left to watch for.
- The update check makes a background request to GitHub's public API on
  every launch. It's designed to never delay the game or the popup closing,
  and fails silently if there's no internet access or GitHub is unreachable
  - but it is still a network call made each time you launch a game.
