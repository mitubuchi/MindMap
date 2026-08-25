namespace MindMap.Abstractions.Tools;

/// <summary>
/// マップのルートノードに書き込む内容。ツールは <see cref="MapToolResult.Root"/> に入れて返す。
///
/// ルートは常に 1 つあって、増えることも消えることもない。<see cref="MapNodeSpec"/> と違って
/// 見つけ直すための識別子が要らないのはそのため。
///
/// <b>書けば上書きになる。</b> 利用者が付けた名前も置き換わる（1 回の Undo で戻せる）ので、
/// 実行のたびに変わる情報（走査した PC や、その時点のアドレスなど）を出すために使うこと。
/// </summary>
public sealed class MapRootSpec
{
    /// <summary>ルートの 1 行目。</summary>
    public required string Title { get; init; }

    /// <summary>本文。空でもよい。</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// リンク。空にしておくと、既にあるリンクは触らない
    /// （<see cref="MapNodeSpec.Link"/> と同じ扱い）。
    /// </summary>
    public string Link { get; init; } = string.Empty;
}
