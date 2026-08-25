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
  - タイトル脇のアイコンは、リンク先の種類に合わせて変わる。ファイルは関連付けられたアプリの
    アイコン、マインドマップ・フォルダー・メールはそれぞれの線画アイコン、Web はリンク記号
    （種類が分からないものは疑問符）
- **子ノードの切り出し** — ノードを右クリック →「子ノードを別のファイルに保存」で、その部分木を別の
  `.mindmap` ファイルへ。ノードのコピーが切り出し先のルートになり、元のノードとの間に相互リンクが張られる。
  元のファイルからは子ノードが消える（Undo で戻せる）
- **ビューア** — 画面右に開閉できる作業ペイン（`F7`）。選択中のノードに追従する
  - **本文** — その場で書き換えられる。入ってから抜けるまでが 1 回ぶんの Undo になる
  - **リンク** — タブの見出しがリンク先のファイル名になり、その下にフルパスが出る
  - **Markdown / SVG / 画像・動画**は、同梱の MdViewer パッケージが整形して表示
  - リンク先が `.mindmap` なら、ノードの親子関係をタイトルの字下げリストで表示
  - テキストとして読めるファイルはそのまま表示（文字コードは BOM・UTF-8・既定コードページの順に判定）
  - 表示できないものは理由を出し、パスの隣のアイコンから関連付けられたアプリに渡せる
  - **種類ごとの表示はパッケージで足せます**（[パッケージ](#パッケージ)）
- **複数ドキュメント** — タブで複数のマップを同時に開ける
- **Undo / Redo** — 追加・削除・編集・移動・リンク設定をまとめて元に戻せる
- **表示** — ズーム（Ctrl+ホイール）、パン（中ボタンドラッグ）、ノードの自動サイズ調整
- **ファイル入出力** — 新規・開く・保存・名前を付けて保存・すべて保存（開いているタブの未保存ぶんをまとめて）。未保存のまま閉じると確認
- **ファイルの関連付け** — `.mindmap` を MindMap で開けるようインストーラーが登録。既定のアプリに設定すれば、ダブルクリックでそのまま開く

## キーボード操作

| キー | 動作 |
|---|---|
| `Ctrl+N` | 新しいタブ |
| `Ctrl+O` | ファイルを開く |
| `Ctrl+S` / `Ctrl+Shift+S` | 保存 / 名前を付けて保存 |
| `Ctrl+Alt+S` | 開いているタブをすべて保存 |
| `Ctrl+W` | タブを閉じる |
| `Ctrl+Z` / `Ctrl+Y` | 元に戻す / やり直し |
| `Ctrl+X` / `Ctrl+C` / `Ctrl+V` | 切り取り / コピー / 貼り付け |
| `Ctrl+A` | すべてのノードを選択 |
| `Tab` / `Insert` | 子ノードを追加 |
| `Enter` | 兄弟ノードを追加 |
| `F2` | 選択中のノードを編集 |
| `Delete` | 選択中のノードとその子孫を削除 |
| `Ctrl` + `+` / `-` / `0` | 拡大 / 縮小 / 等倍 |
| `F7` | ビューアの表示 / 非表示 |

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

# 2. 既定で同梱するパッケージを publish/win-x64/plugins へ配置
#    （MindMapPackages リポジトリ側で実行する）
powershell -ExecutionPolicy Bypass -File ../MindMapPackages/deploy.ps1 -Release

# 3. インストーラーをコンパイル（ISCC.exe のパスは環境に合わせる）
ISCC.exe installer/MindMap.iss
```

`installer/Output/MindMap-1.5.0-Setup.exe` が生成されます。管理者権限なしでユーザー領域に
インストールでき、スタートメニュー登録とアンインストーラーが付きます。

### ファイルの関連付け

インストーラーは `.mindmap` を MindMap に関連付けるための情報を登録します
（管理者権限なしでインストールした場合は、そのユーザーにのみ適用されます）。

ただし Windows 10 / 11 では、既定のアプリをインストーラーから自動で決めることはできません。
初回のみ、次のいずれかで MindMap を選んでください。

- `.mindmap` ファイルを右クリック →「プログラムから開く」→「別のプログラムを選択」→ MindMap を選び、「常にこのアプリを使う」にチェック
- 設定 →「アプリ」→「既定のアプリ」→ ファイルの種類で `.mindmap` を MindMap に設定

以降はダブルクリックでそのまま開けます。コマンドラインからも
`MindMap.exe "path\to\file.mindmap"` の形でファイルを指定して起動できます
（複数指定すると、それぞれ別のタブで開きます）。

### ポータブル ZIP

インストールせずに使いたい場合は、ZIP 版を作成できます（Inno Setup 不要）。

```sh
# 自己完結ビルドを出力してから
dotnet publish src/MindMap/MindMap.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64

# ZIP にまとめる
powershell -ExecutionPolicy Bypass -File installer/package-zip.ps1
```

`installer/Output/MindMap-1.5.0-win-x64.zip` が生成されます。展開してできる `MindMap`
フォルダー内の `MindMap.exe` を実行するだけで動きます。

ビルド済みのインストーラーと ZIP は [Releases](../../releases) からダウンロードできます。

## パッケージ

MindMap は起動時に、実行ファイルの隣にある `plugins` フォルダーを見ます。
そこに置かれたパッケージが名乗った機能を取り込むので、**本体を再ビルドせずに機能が増えます**。

```
MindMap.exe
plugins/
  MdViewer/
    plugin.json          ← 何を提供するかの宣言
    MdViewerPackage.dll  ← 実装
    MdWpf.dll  SvgWpf.dll  ImgWpf.dll  SharpVectors.*.dll
```

`plugins/MdViewer` は**インストーラーと ZIP に同梱されています**。Markdown・SVG・画像・動画の
表示はこれが担当していて、フォルダーごと消せば表示はテキストに落ちます。

### 提供できるもの

| 種類 | 何をするもの | どこに出るか |
|---|---|---|
| **ビューア**（`viewers`） | 選ばれたリンク先を描く | 画面右のビューア |
| **ツール**（`tools`） | 押されたら調べて、結果をマップに重ねる | ツールバーの末尾 |

### 仕組み（ビューア）

```
リンク先 ─→ 拡張子で振り分け ─→ 該当するビューア
                                  └ 無ければテキスト表示（組み込みの受け皿）
```

受け皿が常に引き受けるので、**対応していない種類でも「表示できない」で終わりません。**
呼び出し側にも分岐がありません。

宣言は種類ごとに分かれています。

```json
{
  "id": "com.nwco.mdviewer",
  "apiVersion": "1.0",
  "entry": { "assembly": "MdViewerPackage.dll" },
  "contributes": {
    "viewers": [
      { "type": "MdViewerPackage.MarkdownViewerFactory",
        "extensions": [ ".md", ".markdown" ], "priority": 100 }
    ]
  }
}
```

- **知らない種類は無視されるだけ**で読み込みは止まりません。新しい種類に対応した
  パッケージを古い MindMap に入れても、その種類だけが効かない状態で済みます
- `extensions` を宣言に書いておくと、**該当するファイルが選ばれるまで DLL を読み込みません**。
  重い描画を抱えたパッケージを入れても起動は遅くなりません
- 同じ拡張子を複数が名乗ったら `priority` の大きいほうが使われます
- パッケージごとに読み込み先を分けてあるので、**同梱するライブラリの版がぶつかりません**
- 1 つ壊れていても残りは読み込まれ、理由は起動時に 1 度だけ知らされます

### 仕組み（ツール）

ツールは**マップそのものを触りません**。「ここにこういうノードが欲しい」という木を返すだけで、
実際に作る・書き換える・1 回の Undo にまとめるのは本体側が行います。
パッケージごとに重ね方が違うと、同じ操作でも手で並べ替えた配置が残ったり消えたりするためです。

```
ボタン ─→ ツールが調べる ─→ 識別子つきの木を返す ─→ 本体がマップに重ねる
                                                      └ 1 回の Undo で戻せる
```

同じ識別子（`key`）のノードが既にあれば、本体は**作り直さず中身だけを書き換えます**。

- **位置は動かしません** — 手で並べ替えた配置はそのまま残ります
- **手で足した子ノードも残ります**
- **手で設定したリンクは、ツールが空を返した場合そのままです**
- **結果に出てこなくなったノードは消しません** — 見つからなかっただけなのか、
  無くなったのかを本体は判断できないためです

識別子はノードの「知らない欄」の中に持たせます。本体の欄は増えないので、
**パッケージを入れていない版で開いて保存し直しても、ツールは次にそのノードを見つけ直せます。**

置き場所は宣言で選べます。

```json
"nodeKey": "ExampleKey"
```

| 書き方 | 置き場所 | いつ使うか |
|---|---|---|
| 省略（既定） | `"Extensions": { "<パッケージ ID>": { "key": "…" } }` | ふつうはこちら。名前がぶつからない |
| 欄名を書く | ノードの直下（`"ExampleKey": "…"`） | 同じ形式のファイルを読み書きする**別のプログラムと、同じ欄を使いたいとき** |

欄名を書く場合、名前は自分で選ぶことになるので、ぶつからない名前かどうかは書く側の責任です。
本体が使っている欄の名前（`Title` など）は指定できません。読むときは本体の欄に吸われ、
書くときは同じ名前が 2 つある JSON になってしまうため、**読み込み時に理由を出して止めます**。

名前・アイコン・ショートカットは宣言側に書きます。ここまでは DLL を読まずに分かるので、
**ボタンが押されるまでツールの DLL は読み込まれません。**

```json
"contributes": {
  "tools": [
    { "type": "パッケージ名.型名",
      "title": "ツールの名前", "description": "ツールチップの 2 行目",
      "icon": "M4,11 A11,11 0 0 1 20,11", "shortcut": "F5" }
  ]
}
```

`shortcut` が本体のキーと重なった場合は本体が勝ちます
（パッケージを入れただけで本体の操作が変わってしまわないようにするためです）。

### 作り方

`src/MindMap.Abstractions` を参照して、提供したい種類の取り決めを実装し、
`plugin.json` に型名を書くだけです。

- **ビューア** — `IContentViewerFactory` と `IContentViewer`。
  `IContentViewer.View` が返した `FrameworkElement` が、そのままビューアの枠に入ります
- **ツール** — `IMapTool`。置きたいノードを `MapNodeSpec` の木にして返します。
  自前のダイアログを出すときは、親ウィンドウが `MapToolContext.Owner` で渡されます

```csharp
public sealed class ExampleTool : IMapTool
{
    public async Task<MapToolResult> RunAsync(MapToolContext context, CancellationToken token)
    {
        context.Progress.Report("調べています…");   // ステータスバーに出る

        return new MapToolResult
        {
            Message = "3 件見つかりました",
            Nodes = [new MapNodeSpec { Key = "example:1", Title = "見つかったもの" }],
        };
    }
}
```

既定で同梱しているパッケージの実装は
[MindMapPackages](https://github.com/mitubuchi/MindMapPackages)（非公開）にあります。

## ファイル形式

`.mindmap` ファイルは JSON です。ノードは親子関係を `ParentId` で表すフラットな配列として
保持します。

形式のバージョン（現在 **8**）は、どちら向きにも壊れないように扱います。

- **欠けている欄** — 既定値（文字列は空、数値は 0）を当てます。古い版のファイルはそのまま開けます
- **知らない欄** — 読み飛ばさず、読んだままの形で持っておき、**保存時にそのまま書き戻します**。
  新しい版や、あとから足したパッケージが書いた欄を、それを知らない版で開いて保存しただけで
  失うことがありません

読み込みはバージョン番号で分岐していません。番号は「何を足したか」の記録として持ちます。

```
1 = タイトルのみ（Text 欄）
2 = タイトルと内容に分離（Title / Body 欄）
3 = リンク（Link 欄）
4 = 小さく表示するか（Collapsed 欄）
5 = 制作日・更新日（CreatedAt / UpdatedAt 欄）
7 = 知らない欄をそのまま持ち越す
8 = パッケージのツールが作ったノードに識別子を持たせる（Extensions 欄）
```

6 は、この形式を共有する別のプログラムが独自の欄のために使っているので飛ばしてあります。
同じ番号が 2 つの意味を持つと、ファイルを見ただけでどちらか分からなくなるためです。
その欄は MindMap から見れば「知らない欄」なので、開いて保存し直しても失われません。

あとから欄を足す側は、他と衝突しないよう ID で名前空間を切ってください。

```json
{ "Title": "raspberrypi",
  "Extensions": { "com.nwco.devicemap": { "key": "dev:192.168.1.10" } } }
```

## プロジェクト構成

```
src/
├─ MindMap.Abstractions/   パッケージが参照する契約（これだけが公開の窓口）
└─ MindMap/

src/MindMap/
├─ Models/           保存されるデータ構造
├─ Services/         ファイル入出力・リンク解釈
│  ├─ Viewers/       リンク先の種類ごとの表示
│  ├─ Tools/         パッケージが足す操作と、その結果に付ける識別子
│  └─ Packages/      plugins の走査と DLL の読み込み
├─ ViewModels/       画面ロジック（アプリ全体 / ドキュメント / ノード / ビューア）
├─ Converters/       XAML 用のコンバーター
├─ Undo/             Undo/Redo の履歴管理
├─ Resources/        アイコンなどのリソース
├─ App.xaml          エントリポイント
├─ MainWindow.xaml   メインウィンドウ
└─ ViewerPane.xaml   画面右のビューア
```

## ライセンス

MIT License — [LICENSE](LICENSE) を参照してください。

配布物（インストーラーおよび ZIP）には第三者のソフトウェアが同梱されています。
それぞれの条項と著作権表示は [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) にあります。

| ソフトウェア | ライセンス | 用途 | 出どころ |
|---|---|---|---|
| [SharpVectors](https://github.com/ElinamLLC/SharpVectors) | BSD-3-Clause | SVG の描画 | パッケージ |
| [MdViewer](https://github.com/mitubuchi/MdViewer) | MIT | Markdown・画像・動画の描画 | パッケージ |
| [ReactiveUI](https://github.com/reactiveui/ReactiveUI) | MIT | MVVM | 本体 |
| [System.Reactive](https://github.com/dotnet/reactive) | MIT | Reactive Extensions | 本体 |
| [Splat](https://github.com/reactiveui/splat) | MIT | サービス解決とログ | 本体 |
| [DynamicData](https://github.com/reactivemarbles/DynamicData) | MIT | コレクションの変更通知 | 本体 |
| [.NET ランタイム](https://github.com/dotnet/runtime) | MIT | 自己完結ビルドのため同梱 | 本体 |

**`plugins\MdViewer` を削除すれば、SharpVectors と MdViewer は配布物から外れます。**
その場合これらの条項は適用されません（Markdown や画像の表示は、テキスト表示に戻ります）。
「本体」の行はパッケージの有無にかかわらず含まれます。
