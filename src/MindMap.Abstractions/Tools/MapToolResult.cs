namespace MindMap.Abstractions.Tools;

/// <summary>
/// ツールが持ち帰るもの。マップに重ねたいノードの木と、ステータスバーに出す一行。
/// </summary>
public sealed class MapToolResult
{
    /// <summary>何も置かないとき。取り消された場合や、見つからなかった場合に返す。</summary>
    public static MapToolResult Nothing { get; } = new();

    /// <summary>
    /// ステータスバーに出す一行。null ならホストが「追加 n / 更新 n」を出す。
    /// 件数以外に伝えたいこと（どのネットワークを見たか、など）があるときに使う。
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// ルートノードの子として重ねる木。空でもよい（<see cref="Message"/> だけを出せる）。
    /// </summary>
    public IReadOnlyList<MapNodeSpec> Nodes { get; init; } = [];

    /// <summary>
    /// ルートノード自身に書き込む内容。null なら触らない。
    ///
    /// マップ全体が何のマップなのか（どの PC から見た何を並べたものか）は、
    /// 木の中ではなくルートに出したいことがあるため。上書きになるので、
    /// 実行のたびに変わる情報を出すために使う。
    /// </summary>
    public MapRootSpec? Root { get; init; }
}
