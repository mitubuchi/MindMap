namespace MindMap.Services.Tools;

/// <summary>
/// パッケージが名乗ったツールの置き場。<see cref="Viewers.ViewerRegistry"/> と同じ形で、
/// 提供物の種類ごとにレジストリを 1 つ持つ、という並びをそろえてある。
///
/// 組み込みのツールは無い（本体の操作はツールバーに直接置いてある）ので、
/// 空のまま始まり、パッケージが入っていれば増える。
/// </summary>
public sealed class MapToolRegistry
{
    private readonly List<PackageTool> _tools = [];

    /// <summary>入っているツール。マニフェストを読んだ順（パッケージのフォルダー名順）に並ぶ。</summary>
    public IReadOnlyList<PackageTool> Tools => _tools;

    /// <summary>
    /// ツールを足す。同じ <see cref="PackageTool.Id"/> は後から来たほうで置き換える
    /// （ビューアと同じ扱い）。
    /// </summary>
    public void Add(PackageTool tool)
    {
        _tools.RemoveAll(t => t.Id == tool.Id);
        _tools.Add(tool);
    }
}
