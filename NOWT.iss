#define public Dependency_NoExampleSetup
#include ".\InnoDependencyInstaller\CodeDependencies.iss"

[Setup]
#define MyAppSetupName 'NOWT'
#define MyAppVersion '1.3.5'
#define MyAppPublisher 'PWall'
#define MyAppCopyright 'Soneliem & PWall'
#define MyAppURL 'https://github.com/pwall2222/NOWT'

AppName={#MyAppSetupName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppSetupName} {#MyAppVersion}
AppCopyright={#MyAppCopyright}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
OutputBaseFilename={#MyAppSetupName}
DefaultGroupName={#MyAppSetupName}
DefaultDirName={autopf}\{#MyAppSetupName}
UninstallDisplayIcon=..\NOWT\Assets\logo.ico
SetupIconFile=..\NOWT\Assets\logo.ico
SourceDir=inno
OutputDir=out
AllowNoIcons=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; remove next line if you only deploy 32-bit binaries and dependencies
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: en; MessagesFile: "compiler:Default.isl"

[Files]
; #ifdef UseNetCoreCheck
; download netcorecheck.exe: https://go.microsoft.com/fwlink/?linkid=2135256
; download netcorecheck_x64.exe: https://go.microsoft.com/fwlink/?linkid=2135504
Source: "netcorecheck.exe"; Flags: dontcopy noencryption
Source: "netcorecheck_x64.exe"; Flags: dontcopy noencryption
; #endif

;#ifdef UseDirectX
;Source: "dxwebsetup.exe"; Flags: dontcopy noencryption
;#endif

Source: "NOWT.exe"; DestDir: "{app}"; DestName: "NOWT.exe"; Check: Dependency_IsX64; Flags: ignoreversion
Source: "WebView2Loader.dll"; DestDir: "{app}"; DestName: "WebView2Loader.dll"; Flags: ignoreversion
[Icons]
Name: "{group}\{#MyAppSetupName}"; Filename: "{app}\NOWT.exe"
Name: "{group}\{cm:UninstallProgram,{#MyAppSetupName}}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#MyAppSetupName}"; Filename: "{app}\NOWT.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Run]
Filename: "{app}\NOWT.exe"; Description: "{cm:LaunchProgram,{#MyAppSetupName}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup: Boolean;
begin
  ExtractTemporaryFile('netcorecheck_x64.exe');
  Dependency_AddDotNet60Desktop;
  Result := True;
end;
