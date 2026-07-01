; TimeBarX — Inno Setup script
;
; Build the app first: pwsh scripts/publish.ps1 [-Runtime win-x64|win-arm64]
; Then compile:
;   iscc scripts/installer.iss                     ; x64 (default)
;   iscc /DArch=arm64 scripts/installer.iss        ; ARM64
;
; The Arch flag drives both which artifacts/publish-<rid>/ dir is packaged and
; which OutputBaseFilename Inno emits, so the two architectures don't clobber
; each other's outputs.

#define AppName "TimeBarX"
#define AppVersion "0.1.0"
#define Publisher "TimeBarX"
#define ExeName "TimeBarX.App.exe"

; Default to x64 when the -DArch flag isn't set.
#ifndef Arch
  #define Arch "x64"
#endif

#if Arch == "arm64"
  #define OutputName "TimeBarX-Setup-arm64"
  #define PublishDir "..\artifacts\publish-win-arm64"
  #define ArchAllowed "arm64"
  #define ArchInstallIn64Bit "arm64"
#else
  #define OutputName "TimeBarX-Setup"
  #define PublishDir "..\artifacts\publish-win-x64"
  #define ArchAllowed "x64compatible"
  #define ArchInstallIn64Bit "x64compatible"
#endif

[Setup]
AppId={{B89A5D2A-1F4B-4E6A-9D7A-TIMEBARX0001}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
; Filename is stable across versions; the version lives in the URL path
; (downloads.gettimebarx.com/<version>/TimeBarX-Setup(-arm64)?.exe) and in
; AppVersion above (Add/Remove Programs / Partner Center read it from the manifest).
OutputBaseFilename={#OutputName}
Compression=lzma2
SolidCompression=yes
; Refuse to install on the wrong architecture — Windows will offer to fetch the
; correct package from the Store instead of running the emulated fallback.
ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode={#ArchInstallIn64Bit}
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#ExeName}
SetupIconFile=..\assets\icon.ico
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startmenuicon"; Description: "Create a Start Menu shortcut"; GroupDescription: "Additional shortcuts:"
Name: "startuprun"; Description: "Start TimeBarX when I sign in"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
; Exclude PDB symbols from the installer — they ship debug info, bloat the
; installer by ~5–15 MB, and aren't needed for production users. Keep them in
; the publish dir for local debugging; just don't bundle them.
Source: "{#PublishDir}\*"; DestDir: "{app}"; \
  Excludes: "*.pdb"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#ExeName}"; Tasks: startmenuicon

[Registry]
; Register the timebarx:// URI scheme so launchers and shortcuts can drive the timer.
Root: HKCU; Subkey: "Software\Classes\timebarx"; ValueType: string; ValueName: ""; ValueData: "URL:TimeBarX Protocol"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\timebarx"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\timebarx\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#ExeName},1"
Root: HKCU; Subkey: "Software\Classes\timebarx\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#ExeName}"" ""%1"""

; Optional run-at-startup.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TimeBarX"; ValueData: """{app}\{#ExeName}"""; Flags: uninsdeletevalue; Tasks: startuprun

[Run]
Filename: "{app}\{#ExeName}"; Description: "Launch TimeBarX"; Flags: nowait postinstall skipifsilent
