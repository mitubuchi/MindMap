; Inno Setup スクリプト — MindMap の Windows インストーラー
; 使い方: publish/win-x64 に自己完結ビルドを出力してから
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\MindMap.iss
; を実行すると installer\Output に Setup.exe が作られる。

#define MyAppName "MindMap"
#define MyAppVersion "1.5.0"
#define MyAppPublisher "MindMap"
#define MyAppExeName "MindMap.exe"
; 拡張子と、その種類を表す内部名（ProgID）。
#define MyAppExtension ".mindmap"
#define MyAppProgId "MindMap.Document"
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
; 関連付けを変えるので、エクスプローラーにアイコンの更新を知らせる。
ChangesAssociations=yes
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
; 同梱している第三者ソフトウェアの表示。SharpVectors が BSD-3-Clause なので
; バイナリで再配布する側に表示義務がある。
Source: "{#RepoRoot}\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; .mindmap を MindMap で開けるようにする。HKA は管理者なら HKLM、そうでなければ
; HKCU に書かれるので、権限を上げずにインストールした場合はそのユーザーにだけ適用される。
;
; なお Windows 10/11 では、既定のアプリをインストーラーから勝手に決めることはできない。
; ここで登録するのは「選べる状態にする」ところまでで、既定にするかどうかは
; 「プログラムから開く」や設定画面でユーザーが選ぶ。
Root: HKA; Subkey: "Software\Classes\{#MyAppProgId}"; ValueType: string; ValueName: ""; ValueData: "MindMap マインドマップ"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#MyAppProgId}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
; %1 に開くファイルのパスが入る。空白を含むパスのために引用符で囲む。
Root: HKA; Subkey: "Software\Classes\{#MyAppProgId}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; 拡張子の既定の種類。すでに他のアプリが握っている場合に備え、OpenWithProgids にも足して
; 「プログラムから開く」の一覧に必ず出るようにする。
Root: HKA; Subkey: "Software\Classes\{#MyAppExtension}"; ValueType: string; ValueName: ""; ValueData: "{#MyAppProgId}"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\{#MyAppExtension}\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgId}"; ValueData: ""; Flags: uninsdeletevalue

; アプリ自体の登録。「プログラムから開く」の候補として拾われるようにする。
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: "{#MyAppExtension}"; ValueData: ""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
