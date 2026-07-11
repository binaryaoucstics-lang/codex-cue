#ifndef SourceRoot
  #error SourceRoot define is required
#endif
#ifndef OutputDir
  #error OutputDir define is required
#endif

#define AppName "Codex Option Prompts"
#define AppVersion "1.3.0"
#define AppPublisher "binaryaoucstics-lang"

[Setup]
AppId={{C0D3A5B8-8705-4B9A-A9A3-0A55E2A4B222}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\CodexOptionPrompts
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
OutputDir={#OutputDir}
OutputBaseFilename=CodexOptionPrompts-Setup-x64
SetupIconFile=assets\CodexOptionPrompts.ico
InfoAfterFile=assets\HOOK-TRUST.txt
UninstallDisplayIcon={app}\CodexOptionPrompts.ico
DisableProgramGroupPage=yes
DisableReadyMemo=no
DisableWelcomePage=no
ChangesEnvironment=no
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousGroup=yes
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Files]
Source: "{#SourceRoot}\CodexOptionPrompts.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\NOTICE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\plugins\codex-option-prompts\*"; DestDir: "{app}\plugins\codex-option-prompts"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "assets\CodexOptionPrompts.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\CodexOptionPrompts.exe"; IconFilename: "{app}\CodexOptionPrompts.ico"; WorkingDir: "{app}"

[Run]
Filename: "{app}\CodexOptionPrompts.exe"; Parameters: "--install-plugin"; WorkingDir: "{app}"; StatusMsg: "Installing the Codex plugin..."; Flags: runhidden waituntilterminated

[UninstallRun]
Filename: "{app}\CodexOptionPrompts.exe"; Parameters: "--uninstall-plugin"; WorkingDir: "{app}"; RunOnceId: "UninstallCodexOptionPromptsPlugin"; Flags: runhidden waituntilterminated skipifdoesntexist
