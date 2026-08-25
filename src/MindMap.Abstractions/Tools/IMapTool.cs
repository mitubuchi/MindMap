namespace MindMap.Abstractions.Tools;

/// <summary>
/// マップに何かを足す操作 1 つぶん。ツールバーのボタン 1 つに対応する。
///
/// ビューア（<see cref="Viewers.IContentViewer"/>）が「選ばれたものを描く」のに対して、
/// こちらは「押されたら調べて、その結果をマップに重ねる」もの。
///
/// マップそのものは触らせない。ツールは何を置きたいかを
/// <see cref="MapToolResult"/> として返すだけで、実際にノードを作る・書き換える・
/// 1 回の Undo にまとめる、はホスト側が行う。パッケージごとに重ね方が違うと、
/// 同じ操作でも手で並べ替えた配置が残ったり消えたりしてしまうため。
///
/// 名前・アイコン・ショートカットはマニフェスト（plugin.json）に書く。
/// そうしておくと、ボタンを押すまで DLL を読み込まずに済む。
/// </summary>
public interface IMapTool
{
    /// <summary>
    /// 実行する。UI スレッドで呼ばれるので、時間のかかる処理は await で譲ること。
    ///
    /// 何も置かないとき（利用者が取り消した・見つからなかった）は
    /// <see cref="MapToolResult.Nothing"/> を返す。理由を伝えたいときは
    /// <see cref="MapToolResult.Message"/> に一行入れると、そのままステータスバーに出る。
    ///
    /// 例外を投げるとホストが受け止めてダイアログで見せる。利用者に伝わるのは
    /// その文言だけなので、投げるなら理由が分かる文言にすること。
    /// </summary>
    Task<MapToolResult> RunAsync(MapToolContext context, CancellationToken cancellationToken);
}
