namespace MindMap.Abstractions.Tools;

/// <summary>
/// ツールが「ここにこういうノードが欲しい」と言うための形。ノードそのものではない。
///
/// <see cref="Key"/> が同じノードが既にあれば、ホストは作り直さず中身だけを書き換える。
/// 位置も、手で足した子ノードも、そのまま残る。結果に出てこなくなったノードは消さない
/// （見つからなかっただけなのか、無くなったのかを、ホストは判断できないため）。
/// </summary>
public sealed class MapNodeSpec
{
    /// <summary>
    /// 次に実行したときも同じノードを見つけるための識別子。パッケージごとに名前空間が
    /// 切られるので、他のパッケージのものとぶつかることは気にしなくてよい。
    ///
    /// 見た目（名前）ではなく、変わりにくいものから決めること。名前から決めると、
    /// 相手の名前が変わっただけで別のノードとして増えてしまう。
    /// </summary>
    public required string Key { get; init; }

    /// <summary>ノードの 1 行目。</summary>
    public required string Title { get; init; }

    /// <summary>本文。空でもよい。</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// リンク。空にしておくと、既にあるノードのリンクは触らない
    /// （利用者が手で設定したリンクを、実行のたびに消さないため）。
    /// </summary>
    public string Link { get; init; } = string.Empty;

    /// <summary>このノードの子。木のまま渡せば、ホストが同じ形にする。</summary>
    public IReadOnlyList<MapNodeSpec> Children { get; init; } = [];
}
