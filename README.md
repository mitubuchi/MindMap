# MindMap

C# / WPF 製のマインドマップ作成アプリケーション。ノードを自由に配置してツリー状に
つなぎ、`.mindmap`（JSON）ファイルとして保存・読み込みできます。

MVVM フレームワークに [ReactiveUI](https://www.reactiveui.net/) を使用しています。

## 主な機能

- **ノード編集** — 追加（子 / 兄弟）、その場編集、部分木ごとの削除、ドラッグでの移動
- **複数選択** — Ctrl / Shift + クリック、余白のドラッグ（範囲選択）、Ctrl+A（全選択）で複数のノードをまとめて選択・移動
- **切り取り / コピー / 貼り付け** — 選択したノードを部分木ごと。タブや別ウィンドウの MindMap をまたいで貼り付けられる（クリップボード経由）
- **タイトルと内容** — 1 行のタイトル（中央寄せ）と、複数行の内容（左寄せ）に分離
- **リンク** — ノードに URL やファイルへのリンクを設定
  - URL は既定のブラウザーで開く
  - ファイルは関連付けられたアプリで開く（関連付けが無ければ「プログラムから開く」を表示）
  - リンク先が `.mindmap` ファイルなら、新しいタブで開く
  - 設定は右クリックメニュー・ファイル選択・ブラウザー/エクスプローラーからのドラッグに対応
- **複数ドキュメント** — タブで複数のマップを同時に開ける
- **Undo / Redo** — 追加・削除・編集・移動・リンク設定をまとめて元に戻せる
- **表示** — ズーム（Ctrl+ホイール）、パン（中ボタンドラッグ）、ノードの自動サイズ調整
- **ファイル入出力** — 新規・開く・保存・名前を付けて保存。未保存のまま閉じると確認

## キーボード操作

| キー | 動作 |
|---|---|
| `Ctrl+N` | 新しいタブ |
| `Ctrl+O` | ファイルを開く |
| `Ctrl+S` / `Ctrl+Shift+S` | 保存 / 名前を付けて保存 |
| `Ctrl+W` | タブを閉じる |
| `Ctrl+Z` / `Ctrl+Y` | 元に戻す / やり直し |
| `Ctrl+X` / `Ctrl+C` / `Ctrl+V` | 切り取り / コピー / 貼り付け |
| `Ctrl+A` | すべてのノードを選択 |
| `Tab` / `Insert` | 子ノードを追加 |
| `Enter` | 兄弟ノードを追加 |
| `F2` | 選択中のノードを編集 |
| `Delete` | 選択中のノードとその子孫を削除 |
| `Ctrl` + `+` / `-` / `0` | 拡大 / 縮小 / 等倍 |

ノードの編集中は、タイトル欄で `Enter` を押すと内容欄へ移動し、`Ctrl+Enter` で確定、
`Esc` で編集前に戻します。

複数選択は、`Ctrl+クリック` で 1 つずつ足し引き、`Shift+クリック` で追加、
何もない余白をドラッグすると範囲選択できます。複数選んだままドラッグすると、まとめて移動します。

## 動作環境

- Windows
- [.NET 9 SDK](https://dotnet.microsoft.com/download)

## ビルドと実行

```sh
dotnet build MindMap.sln
dotnet run --project src/MindMap
```

Visual Studio 2022 で `MindMap.sln` を開いて F5 でも実行できます。

## インストーラーの作成

Windows 用のインストーラー（自己完結・.NET 不要）を作るには、[Inno Setup](https://jrsoftware.org/isinfo.php) が必要です。

```sh
# 1. 自己完結ビルドを出力
dotnet publish src/MindMap/MindMap.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64

# 2. インストーラーをコンパイル（ISCC.exe のパスは環境に合わせる）
ISCC.exe installer/MindMap.iss
```

`installer/Output/MindMap-1.1.0-Setup.exe` が生成されます。管理者権限なしでユーザー領域に
インストールでき、スタートメニュー登録とアンインストーラーが付きます。

### ポータブル ZIP

インストールせずに使いたい場合は、ZIP 版を作成できます（Inno Setup 不要）。

```sh
# 自己完結ビルドを出力してから
dotnet publish src/MindMap/MindMap.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64

# ZIP にまとめる
powershell -ExecutionPolicy Bypass -File installer/package-zip.ps1
```

`installer/Output/MindMap-1.1.0-win-x64.zip` が生成されます。展開してできる `MindMap`
フォルダー内の `MindMap.exe` を実行するだけで動きます。

ビルド済みのインストーラーと ZIP は [Releases](../../releases) からダウンロードできます。

## ファイル形式

`.mindmap` ファイルは JSON です。ノードは親子関係を `ParentId` で表すフラットな配列として
保持します。形式のバージョンは後方互換で、古いバージョンのファイルもそのまま開けます。

## プロジェクト構成

```
src/MindMap/
├─ Models/           保存されるデータ構造
├─ Services/         ファイル入出力・リンク解釈
├─ ViewModels/       画面ロジック（アプリ全体 / ドキュメント / ノード）
├─ Converters/       XAML 用のコンバーター
├─ Undo/             Undo/Redo の履歴管理
├─ Resources/        アイコンなどのリソース
├─ App.xaml          エントリポイント
└─ MainWindow.xaml   メインウィンドウ
```
