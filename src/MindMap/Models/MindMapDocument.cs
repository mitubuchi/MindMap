using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindMap.Models;

/// <summary>
/// ファイルに保存されるマインドマップ全体。ノードは親子関係を <see cref="MindMapNodeDto.ParentId"/> で表す
/// フラットな配列として持つ（入れ子にしないことで、後から親を付け替えても差分が小さく済む）。
/// </summary>
public sealed class MindMapDocument
{
    /// <summary>
    /// ファイル形式のバージョン。
    /// 1 = タイトルのみ（Text 欄）／2 = タイトルと内容に分離（Title / Body 欄）／
    /// 3 = リンク（Link 欄）を追加／4 = 小さく表示するか（Collapsed 欄）を追加／
    /// 5 = 制作日・更新日（CreatedAt / UpdatedAt 欄）を追加／
    /// 7 = 知らない欄をそのまま持ち越す（<see cref="Extra"/>）。
    ///
    /// 6 は DeviceMap が DeviceKey 欄のために使っているので飛ばしてある。
    /// 同じ番号が 2 つの意味を持つと、ファイルを見ただけでどちらか分からなくなるため。
    ///
    /// なお、読み込みはこの番号で分岐していない。欠けている欄は既定値、知らない欄は
    /// <see cref="Extra"/> に入るので、どの版のファイルもそのまま読める。
    /// 番号は「何を足したか」の記録として持つ。
    /// </summary>
    public int Version { get; set; } = CurrentVersion;

    public const int CurrentVersion = 7;

    public List<MindMapNodeDto> Nodes { get; set; } = new();

    /// <summary>
    /// このアプリが知らない、ファイル全体に付いていた欄。読んだままの形で持っておき、
    /// 保存時にそのまま書き戻す（<see cref="MindMapNodeDto.Extra"/> と同じ考え方）。
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class MindMapNodeDto
{
    public Guid Id { get; set; }

    /// <summary>ルートノードの場合は null。</summary>
    public Guid? ParentId { get; set; }

    /// <summary>1 行の見出し。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>改行を含められる本文。空でもよい。</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>URL またはファイルパス。空ならリンクなし。</summary>
    public string Link { get; set; } = string.Empty;

    /// <summary>小さく表示する（タイトルのみ）なら true。既定は false。</summary>
    public bool Collapsed { get; set; }

    /// <summary>
    /// 制作日と更新日。UI には出さず、後のデータ処理のために持つ。
    /// Version 5 より前の古いファイルには無いので null になり、読み込み時に補う。
    /// </summary>
    public DateTimeOffset? CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// バージョン 1 でタイトルが入っていた欄。古いファイルを読むためだけに残してあり、
    /// 保存時は常に null なので書き出されない。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    /// <summary>形式の違いを吸収してタイトルを取り出す。</summary>
    public string ResolveTitle() => string.IsNullOrEmpty(Title) ? Text ?? string.Empty : Title;

    public double X { get; set; }

    public double Y { get; set; }

    /// <summary>
    /// このアプリが知らない欄。読んだままの形で持っておき、保存時にそのまま書き戻す。
    ///
    /// 新しい版や、あとから足したパッケージが書いた欄を、それを知らない版で開いて
    /// 保存しただけで失わないため。中身は解釈しない（知らないものは知らないまま運ぶ）。
    /// 新しく欄を足す側は、他と衝突しないよう "Extensions": { "&lt;パッケージ ID&gt;": { ... } }
    /// のように名前空間を切ること。
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
