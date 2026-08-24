namespace MindMap.Services.Viewers;

/// <summary>
/// リンク先を種類ごとに出すもの。いまは中身をテキストとして返すだけだが、
/// パッケージから足せるようにするときは、ここが「描画そのもの」を返す形に広がる
/// （テキスト以外を描くビューアが出てきて初めて、その形が確かめられるため）。
/// </summary>
public interface IContentViewer
{
    /// <summary>複数が扱えるときの優先順位。大きいほうが勝つ。</summary>
    int Priority { get; }

    bool CanView(ViewerContent content);

    Task<TextDocument> LoadAsync(ViewerContent content, CancellationToken cancellationToken);
}
