#define MyAppName "Steam Loading Popups"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
#define MyAppPublisher "Steam Loading Popups"
#define MyAppExeName "SteamGameLoader.exe"

[Setup]
AppId={{B36F1F1E-6B0A-4C9E-9C1E-9B6F6E1C2B4F}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\SteamGameLoader
DefaultGroupName=Steam Loading Popups
DisableProgramGroupPage=yes
SetupIconFile=..\src\SteamGameLoader\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=output
OutputBaseFilename=SteamGameLoadingPopup-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\publish\SteamGameLoader.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Steam Loading Popups"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall Steam Loading Popups"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Set up Game Loading shortcuts now"; Flags: postinstall nowait skipifsilent
