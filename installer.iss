; MyHobbyWarehouse — Inno Setup installer script
; Run build-setup.ps1 to publish + compile this script into setup.exe.
; Version is injected by build-setup.ps1 via /dMyAppVersion=...

#define MyAppName "MyHobbyWarehouse"
#define MyAppVersion "1.5"
#define MyAppPublisher "Jure Rejc"
#define MyAppURL "https://github.com/jurerejc/EagleManager"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId for other applications.
AppId={{8F3C9A2B-1D4E-4C7A-9B6F-2E5C8A1D0F3B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=installer
OutputBaseFilename={#MyAppName}-setup-{#MyAppVersion}
SetupIconFile=MyHobbyWarehouse\Resources\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppName}.exe

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "sl"; MessagesFile: "compiler:Languages\Slovenian.isl"

[Files]
; Self-contained single-file publish output (just the .exe, .NET bundled inside).
Source: "MyHobbyWarehouse\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppName}.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppName}.exe"

[Run]
; No post-install run; the app configures its own database location on first launch.
