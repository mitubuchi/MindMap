; Inno Setup スクリプト — MindMap の Windows インストーラー
; 使い方: publish/win-x64 に自己完結ビルドを出力してから
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\MindMap.iss
; を実行すると installer\Output に Setup.exe が作られる。

#define MyAppName "MindMap"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "MindMap"
#define MyAppExeName "MindMap.exe"
; リポジトリのルート（このスクリプトの 1 つ上）を基準にする。
#define RepoRoot ".."

[Setup]
; 再インストールやアンインストールで同一アプリと認識させるための一意な ID。
AppId={{8F2B4C7A-6D1E-4B93-A0F5-2C9E7D4B1A63}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
; 管理者権限を求めず、ユーザー領域にインストールする（UAC なしで入れられる）。
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=yes
OutputDir={#RepoRoot}\installer\Output
OutputBaseFilename=MindMap-{#MyAppVersion}-Setup
SetupIconFile={#RepoRoot}\src\MindMap\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 自己完結ビルドの中身をまるごと入れる。
Source: "{#RepoRoot}\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
