# Steam Loading Popups

Shows a "Game Loading…" popup with a spinner the moment you launch a game from
its Steam desktop shortcut, and closes itself automatically as soon as the
game has actually started — covering the gap between double-clicking the
shortcut and the game window appearing.

## How it works

1. **Setup (one-time):** run the app with no arguments (Start Menu shortcut
   "Steam Loading Popups", installed alongside it) to open the setup
   tool. It scans your Desktop for existing Steam game shortcuts (the ones
   Steam creates via *right-click a game → Manage → Add desktop shortcut*) —
   whether that's a `.url` Internet Shortcut (`steam://rungameid/<id>`), a
   `.lnk` pointing at `steam.exe -applaunch <id>`, or a `.lnk` pointing
   straight at the game's `.exe` (the App ID is then recovered by matching the
   exe's folder against Steam's own `appmanifest_*.acf` files) — and lets you
   convert the ones you want. Converting a shortcut rewrites it to point at
   this app with the game's App ID instead, while keeping the same name and
   icon (a `.url` shortcut becomes a `.lnk`, since only `.lnk` files can pass
   arguments to a program). The original is backed up so you can restore it
   later from the same setup tool.
2. **Launch:** double-click a converted shortcut. The app immediately starts
   the game via `steam://rungameid/<appid>` and shows the loading popup.
3. **Detection:** Steam itself sets
   `HKCU\Software\Valve\Steam\Apps\<appid>\Running = 1` the instant it hands
   off to the game (and back to `0` when it exits). The popup polls that flag
   twice a second and closes as soon as it flips to `1` — this works for any
   game without needing to know its actual `.exe` name, and also covers cold
   starts where Steam itself has to launch first.
4. **Safety net:** the popup also has a Cancel button and auto-closes after
   90 seconds regardless, so it can never get stuck open if detection doesn't
   fire (e.g. Steam is offline, or the app id is wrong).

## Installing

Download the latest `SteamGameLoadingPopup-Setup.exe` from the
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
installer, and attach `SteamGameLoadingPopup-Setup.exe` to a GitHub Release
for that tag automatically (creating the release if it doesn't exist yet, or
updating it - e.g. adding the asset - if it does).

You can also trigger it manually from the Actions tab (`workflow_dispatch`)
and type a version number (e.g. `1.0.0`) into the "Run workflow" box - this
does exactly the same create-or-update against a release with that tag name,
which is handy if you already created the release by hand from the GitHub UI.
Leaving the version box at its default `0.0.0-dev` just builds and uploads a
throwaway build artifact, without touching any release - useful for
verifying the app still compiles without publishing anything.

## Limitations

- Windows only, and only wraps *existing* Steam desktop shortcuts — it does
  not intercept launches from inside the Steam client itself.
- For games that hand off to a separate launcher or anti-cheat process before
  the actual game starts, Steam's `Running` flag (and so the popup) may close
  slightly before the game's own window appears.
