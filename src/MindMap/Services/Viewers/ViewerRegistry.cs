namespace MindMap.Services.Viewers;

/// <summary>
/// リンク先の種類ごとに、どのビューアで出すかを決める。
/// いまは組み込みだけを持つが、パッケージから足せるようにするときも、
/// ここに登録が増えるだけで呼ぶ側は変わらない形にしてある。
/// </summary>
public sealed class ViewerRegistry
{
    private readonly List<IContentViewer> _viewers;

    public ViewerRegistry(IEnumerable<IContentViewer>? viewers = null) =>
        _viewers = (viewers ?? Builtin()).OrderByDescending(v => v.Priority).ToList();

    /// <summary>
    /// 扱えるビューアのうち、いちばん優先度が高いもの。
    /// 受け皿の <see cref="PlainTextViewer"/> がすべてを引き受けるので、必ず 1 つ返る。
    /// </summary>
    public IContentViewer Resolve(ViewerContent content) => _viewers.First(v => v.CanView(content));

    private static IEnumerable<IContentViewer> Builtin()
    {
        yield return new MindMapOutlineViewer();
        yield return new PlainTextViewer();
    }
}
