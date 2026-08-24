using System.IO;

namespace MindMap.Abstractions.Viewers;

/// <summary>
/// ビューアに渡す、出す対象。いまはノードのリンク先のファイルだけ。
///
/// 種類を決める材料（<see cref="Extension"/>）をここで用意しておくのは、
/// パッケージごとに拡張子の取り出し方が違うと、同じファイルでも
/// 選ばれるビューアが変わってしまうため。
/// </summary>
/// <param name="FilePath">絶対パス。ホスト側で相対リンクを解決済み。</param>
public sealed record ViewerContent(string FilePath)
{
    /// <summary>小文字の拡張子（"." 付き）。拡張子が無ければ空。</summary>
    public string Extension { get; } = Path.GetExtension(FilePath).ToLowerInvariant();

    /// <summary>ファイル名だけ。見出しなどに使う。</summary>
    public string FileName { get; } = Path.GetFileName(FilePath);
}
