using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindMap.Services.Packages;

/// <summary>
/// パッケージ 1 つぶんの plugin.json。
///
/// 提供物は種類ごとに分けて書く（<see cref="PackageContributions"/>）。
/// 種類を増やすときは、ホスト側にその種類のレジストリを 1 つ足し、ここに欄を 1 つ増やすだけで、
/// 既にあるパッケージにも読み込みの流れにも手を入れずに済む。
/// </summary>
public sealed class PackageManifest
{
    /// <summary>他と重ならない名前。"com.nwco.mdviewer" のような逆ドメイン形式にする。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>画面や記録に出す名前。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>パッケージ自身の版。ホストは中身を見ず、記録に出すだけ。</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 想定している契約（MindMap.Abstractions）の版。
    /// 大版が食い違うパッケージは読み込まない。
    /// </summary>
    public string ApiVersion { get; set; } = string.Empty;

    public PackageEntry? Entry { get; set; }

    /// <summary>
    /// ツールが作ったノードに付ける識別子を、ノードのどの欄に置くか。
    ///
    /// 省略するのが普通で、その場合は "Extensions" の下にパッケージ ID で名前空間を切って
    /// 置かれる（ぶつかる心配が無い）。ここに名前を書くと、ノードの直下にその名前で置く。
    /// 同じ形式のファイルを読み書きする別のプログラムと、同じ欄を使いたいときのためのもの。
    ///
    /// 本体が使っている欄の名前は指定できない（読み込み時に理由を出して止まる）。
    /// パッケージ全体で 1 つ。同じパッケージの別のツールでも、同じノードを見つけ直せるようにする。
    /// </summary>
    public string? NodeKey { get; set; }

    public PackageContributions Contributes { get; set; } = new();
}

/// <summary>提供物が入っている DLL。</summary>
public sealed class PackageEntry
{
    /// <summary>パッケージのフォルダーからの相対名。依存 DLL は .deps.json から解決される。</summary>
    public string Assembly { get; set; } = string.Empty;
}

/// <summary>
/// このパッケージが提供するものを、種類ごとに分けて並べたもの。
///
/// 知らない種類は <see cref="Extra"/> に落ちるだけで、読み込みは止まらない。
/// 新しい種類に対応したパッケージを、まだ対応していないホストに入れても
/// 「その種類だけが効かない」で済むようにするため。
/// </summary>
public sealed class PackageContributions
{
    /// <summary>リンク先の表示。</summary>
    public List<ViewerContribution> Viewers { get; set; } = [];

    /// <summary>ツールバーに並ぶ操作。</summary>
    public List<ToolContribution> Tools { get; set; } = [];

    /// <summary>ノードに出す、リンク先の小さな絵。</summary>
    public List<ThumbnailContribution> Thumbnails { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>ビューアを 1 つ提供する宣言。</summary>
public sealed class ViewerContribution
{
    /// <summary><c>IContentViewerFactory</c> を実装した型の完全名。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 扱える拡張子（"." 付き）。ここに書いておくと、該当するファイルが選ばれるまで
    /// DLL を読み込まずに済む。
    /// </summary>
    public List<string> Extensions { get; set; } = [];

    /// <summary>同じ拡張子を複数のパッケージが名乗ったときの優先順位。大きいほうが勝つ。</summary>
    public int Priority { get; set; } = 100;
}

/// <summary>サムネイルを 1 つ提供する宣言。</summary>
public sealed class ThumbnailContribution
{
    /// <summary><c>INodeThumbnailProvider</c> を実装した型の完全名。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 扱える拡張子（"." 付き）。ここに書いておくと、該当するリンクが現れるまで
    /// DLL を読み込まずに済む。
    /// </summary>
    public List<string> Extensions { get; set; } = [];

    /// <summary>同じ拡張子を複数のパッケージが名乗ったときの優先順位。大きいほうが勝つ。</summary>
    public int Priority { get; set; } = 100;
}

/// <summary>
/// ツールを 1 つ提供する宣言。
///
/// 名前・アイコン・ショートカットをここに書くのは、ツールバーを組み立てるために
/// DLL を読み込まずに済ませるため（押されるまで読み込まない）。
/// </summary>
public sealed class ToolContribution
{
    /// <summary><c>IMapTool</c> を実装した型の完全名。引数の無いコンストラクタが要る。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>ツールバーのツールチップに出す名前。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>名前だけでは足りないときの補足。ツールチップの 2 行目に出る。</summary>
    public string? Description { get; set; }

    /// <summary>
    /// アイコンの図形。WPF の Path と同じミニ言語（"M12,17 A1.6,1.6 0 0 1 …"）で、
    /// 24x24 を目安に線だけで描く（塗りは付かない）。省略すると既定の印になる。
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// "F5" や "Ctrl+Shift+D" のようなキー。省略するとショートカットは作らない。
    /// 本体が使っているキーと重なった場合は、本体側が勝つ。
    /// </summary>
    public string? Shortcut { get; set; }
}
