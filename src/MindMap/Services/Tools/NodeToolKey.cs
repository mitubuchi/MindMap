using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MindMap.Models;
using MindMap.ViewModels;

namespace MindMap.Services.Tools;

/// <summary>
/// ツールが作ったノードに、次も同じノードを見つけ直すための識別子を持たせる。
///
/// 置き場所は 2 通りある。どちらもノードの「知らない欄」
/// （<see cref="MindMapNodeDto.Extra"/>）の中なので、本体の欄は増えない。
///
/// <list type="bullet">
/// <item>
/// 既定 — <c>"Extensions": { "&lt;パッケージ ID&gt;": { "key": "…" } }</c>。
/// パッケージ ID で区切ってあるので、2 つのパッケージが同じ識別子を名乗っても混ざらない。
/// </item>
/// <item>
/// マニフェストで欄名を指定した場合 — ノードの直下にその名前で置く（<c>"DeviceKey": "…"</c>）。
/// 同じ形式のファイルを読み書きする別のプログラムに合わせるためのもの。
/// 名前は自分で選ぶことになるので、ぶつからない名前かどうかは書く側の責任になる。
/// </item>
/// </list>
///
/// どちらの場合も、パッケージを入れていない版で開いて保存し直しても持ち越される
/// （知らない欄はそのまま書き戻されるため）。
/// </summary>
internal static class NodeToolKey
{
    private const string Section = "Extensions";
    private const string Field = "key";

    /// <summary>
    /// 欄名として使えない名前。本体が使っている欄の名前と、名前空間を切るための "Extensions"。
    ///
    /// 本体の欄と同じ名前を指定すると、読むときは本体の欄に吸われて
    /// 「知らない欄」に入らず、書くときは同じ名前が 2 つある JSON になってしまう。
    /// 欄が増えても追いかけなくて済むよう、名前は型から拾う。
    /// </summary>
    private static readonly HashSet<string> Reserved = typeof(MindMapNodeDto)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(p => p.Name)
        .Append(Section)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// マニフェストで指定された欄名が使えるか確かめる。使えなければ理由を投げる
    /// （読み込みを止めて知らせる。黙って既定に戻すと、識別子が付いていないように見えて
    /// 実行のたびにノードが増えていく）。
    /// </summary>
    public static void RequireUsable(string packageId, string field)
    {
        if (Reserved.Contains(field))
        {
            throw new InvalidDataException(
                $"{packageId}: nodeKey に {field} は使えません（本体が使っている欄の名前です）。");
        }
    }

    /// <summary>
    /// このパッケージが付けた識別子。付いていなければ null。
    /// <paramref name="field"/> が null なら Extensions の下を見る。
    /// </summary>
    public static string? Get(NodeViewModel node, string owner, string? field)
    {
        if (node.Extra is not { } extra)
        {
            return null;
        }

        // 空文字は「付いていない」と見なす。欄をいつも書き出す作りのプログラムでは、
        // 手で作ったノードに空の識別子が入っているため。
        if (field is { Length: > 0 })
        {
            return extra.TryGetValue(field, out var direct) && direct.ValueKind == JsonValueKind.String
                ? Empty(direct.GetString())
                : null;
        }

        if (!extra.TryGetValue(Section, out var section)
            || section.ValueKind != JsonValueKind.Object
            || !section.TryGetProperty(owner, out var mine)
            || mine.ValueKind != JsonValueKind.Object
            || !mine.TryGetProperty(Field, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return Empty(value.GetString());
    }

    private static string? Empty(string? key) => key is { Length: > 0 } ? key : null;

    /// <summary>
    /// 識別子を付ける。Extensions の下に置く場合、同じ場所にある他のパッケージの欄も、
    /// このパッケージの別の欄も、読んだままの形で残す
    /// （知らないものは知らないまま運ぶ、という扱いをここでも守る）。
    /// </summary>
    public static void Set(NodeViewModel node, string owner, string? field, string key)
    {
        var extra = node.Extra ??= new Dictionary<string, JsonElement>();

        if (field is { Length: > 0 })
        {
            extra[field] = JsonSerializer.SerializeToElement(key);
            return;
        }

        var hasSection = extra.TryGetValue(Section, out var section) && section.ValueKind == JsonValueKind.Object;

        var next = new JsonObject();
        if (hasSection)
        {
            foreach (var property in section.EnumerateObject().Where(p => p.Name != owner))
            {
                next[property.Name] = JsonNode.Parse(property.Value.GetRawText());
            }
        }

        var mine = new JsonObject();
        if (hasSection
            && section.TryGetProperty(owner, out var existing)
            && existing.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in existing.EnumerateObject().Where(p => p.Name != Field))
            {
                mine[property.Name] = JsonNode.Parse(property.Value.GetRawText());
            }
        }

        mine[Field] = key;
        next[owner] = mine;

        extra[Section] = JsonSerializer.SerializeToElement(next);
    }
}
