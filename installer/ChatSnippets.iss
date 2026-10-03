; Установщик ClaudeCode Chat Snippets (Inno Setup 6). Сборка: installer\build.ps1
#define AppName "ClaudeCode Chat Snippets"
#define AppVersion "1.0.2"
#define AppExe "ChatSnippets.exe"

[Setup]
AppId={{7B1C2E5A-3F0D-4C7B-9E21-5A6D8C4F1B90}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Егоров Сергей aka Evpatiy
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Без прав администратора: ставится в профиль пользователя
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=Output
OutputBaseFilename=ClaudeCodeChatSnippets-Setup-{#AppVersion}
SetupIconFile=..\src\ChatSnippets.App\Assets\chat-snippets.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
; Если программа запущена — установщик попросит её закрыть
AppMutex=ChatSnippets.SingleInstance
CloseApplications=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "Запускать при входе в Windows / Start with Windows"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ClaudeCodeChatSnippets"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
