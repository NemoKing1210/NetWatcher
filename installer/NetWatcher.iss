; NetWatcher installer. All version and path values come from /D defines:
;   ISCC.exe installer\NetWatcher.iss /DAppVersionFull=1.2.3 /DAppVersionNumeric=1.2.3 ^
;     /DSourceDir=<abs publish dir> /DOutputDir=<abs output dir>

#ifndef AppVersionFull
  #define AppVersionFull "0.0.0-local"
#endif
#ifndef AppVersionNumeric
  #define AppVersionNumeric "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#define MyAppName "NetWatcher"
#define MyAppPublisher "NemoKing1210"
#define MyAppUrl "https://github.com/NemoKing1210/NetWatcher"
#define MyAppExeName "NetWatcher.exe"

[Setup]
AppId={{B0FAF158-5F30-4E43-86F7-C4349CA2668B}
AppName={#MyAppName}
AppVersion={#AppVersionNumeric}
AppVerName={#MyAppName} {#AppVersionFull}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
AppUpdatesURL={#MyAppUrl}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=..\LICENSE
SetupIconFile=..\assets\logo.ico
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=no
RestartApplications=no
SetupLogging=yes
OutputDir={#OutputDir}
OutputBaseFilename={#MyAppName}-v{#AppVersionFull}-win-x64-setup
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoProductName={#MyAppName}
VersionInfoVersion={#AppVersionNumeric}
VersionInfoProductVersion={#AppVersionNumeric}
VersionInfoTextVersion={#AppVersionFull}

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM {#MyAppExeName} /F"; Flags: runhidden; RunOnceId: "StopNetWatcher"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  { No single-instance mutex in the app, so Inno's AppMutex/Restart Manager cannot
    close it; kill only our own process. 128 means it was not running. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#MyAppExeName} /F', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if (ResultCode <> 0) and (ResultCode <> 128) then
    Result := 'NetWatcher is still running and could not be closed (taskkill exit code ' +
      IntToStr(ResultCode) + '). Close it and run the installer again.'
  else
    Result := '';
end;
