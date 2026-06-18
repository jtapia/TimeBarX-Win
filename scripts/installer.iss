; TimeBarX — Inno Setup script
; Build the app first: pwsh scripts/publish.ps1
; Then compile: iscc scripts/installer.iss

#define AppName "TimeBarX"
#define AppVersion "0.1.0"
#define Publisher "TimeBarX"
#define ExeName "TimeBarX.App.exe"

[Setup]
AppId={{B89A5D2A-1F4B-4E6A-9D7A-TIMEBARX0001}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=TimeBarX-{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
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
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

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
