; PunchThrough Windows Installer — Inno Setup Script
; Bundles PunchThrough.exe (self-contained) + SpoofDPI binary
;
; Build steps:
;   1. dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained
;   2. Download spoofdpi-windows-amd64.exe to installer\bin\spoofdpi.exe
;   3. Compile this .iss with Inno Setup Compiler

#define AppName "PunchThrough"
#define AppVersion "1.1.0"
#define AppPublisher "PunchThrough"
#define AppURL "https://github.com/quardianwolf/PunchThrough-Windows"
#define AppExeName "PunchThrough.exe"

[Setup]
AppId={{B7E8C3A1-4F2D-4E8A-9C1B-3D5F7A8E2B4C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=..\dist
OutputBaseFilename=PunchThrough-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\PunchThrough\Assets\icon.ico
UninstallDisplayIcon={app}\{#AppExeName}
; Minimize install — no start menu group prompt
DisableProgramGroupPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checked
Name: "launchstartup"; Description: "Launch at Windows startup"; GroupDescription: "Options:"; Flags: checked
Name: "autoconnect"; Description: "Auto-connect on startup"; GroupDescription: "Options:"; Flags: checked

[Files]
; Main application (self-contained single file)
Source: "..\publish\win-x64\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; SpoofDPI binary — bundled so user doesn't need to install anything
Source: "bin\spoofdpi.exe"; DestDir: "{localappdata}\PunchThrough\bin"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Launch at startup (optional task)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue; Tasks: launchstartup

[Run]
; Launch after install
Filename: "{app}\{#AppExeName}"; Description: "Launch PunchThrough"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kill PunchThrough and SpoofDPI on uninstall
Filename: "taskkill"; Parameters: "/F /IM PunchThrough.exe"; Flags: runhidden; RunOnceId: "KillPunchThrough"
Filename: "taskkill"; Parameters: "/F /IM spoofdpi.exe"; Flags: runhidden; RunOnceId: "KillSpoofDPI"

[UninstallDelete]
; Clean up SpoofDPI binary, settings, and marker files
Type: filesandordirs; Name: "{localappdata}\PunchThrough"

[Code]
// Write auto-connect setting to settings.json after install
procedure WriteAutoConnectSetting();
var
  SettingsDir, SettingsPath, Json: String;
begin
  SettingsDir := ExpandConstant('{localappdata}\PunchThrough');
  ForceDirectories(SettingsDir);
  SettingsPath := SettingsDir + '\settings.json';

  if IsTaskSelected('autoconnect') then
    Json := '{"AutoConnect": true, "LaunchAtStartup": true, "EnableSystemProxy": true, "EnableDoH": true, "SpoofDpiPort": 8080}'
  else
    Json := '{"AutoConnect": false, "LaunchAtStartup": true, "EnableSystemProxy": true, "EnableDoH": true, "SpoofDpiPort": 8080}';

  // Only write if settings don't exist yet (don't overwrite user config on upgrade)
  if not FileExists(SettingsPath) then
    SaveStringToFile(SettingsPath, Json, False);
end;

// Disable system proxy on uninstall to avoid leaving user with broken internet
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Disable proxy via reg
    RegWriteDWordValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Internet Settings',
      'ProxyEnable', 0);
    RegDeleteValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Internet Settings',
      'ProxyServer');
    RegDeleteValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Internet Settings',
      'ProxyOverride');
    // Remove startup entry
    RegDeleteValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Run',
      '{#AppName}');
    // Refresh WinINet
    Exec('rundll32.exe', 'wininet.dll,InternetSetOption', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    WriteAutoConnectSetting();
end;
