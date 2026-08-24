namespace MindMap.Services.Viewers;

/// <summary>
/// 種類ごとのビューアが見つからなかったときの受け皿。テキストとして読めれば出す。
/// これがあるおかげで、呼ぶ側は「対応するビューアが無い場合」を書かなくて済む。
/// </summary>
public sealed class PlainTextViewer : IContentViewer
{
    /// <summary>受け皿なので、他のどのビューアにも譲る。</summary>
    public int Priority => int.MinValue;

    public bool CanView(ViewerContent content) => true;

    public Task<TextDocument> LoadAsync(ViewerContent content, CancellationToken cancellationToken) =>
        TextDocumentReader.ReadAsync(content.FilePath, cancellationToken);
}
