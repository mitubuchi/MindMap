using System.Reactive;
using MindMap.Services.Tools;
using ReactiveUI;

namespace MindMap.ViewModels;

/// <summary>
/// ツールバーに並ぶ、パッケージのツール 1 つ。名乗り（名前・アイコン・ショートカット）は
/// マニフェストから来ているので、DLL を読み込まずにボタンを組み立てられる。
/// </summary>
public sealed class MapToolViewModel
{
    public MapToolViewModel(PackageTool tool, ReactiveCommand<Unit, Unit> command)
    {
        Tool = tool;
        Command = command;
    }

    public PackageTool Tool { get; }

    /// <summary>ボタンの文言。ラベルとショートカットを 1 つにまとめたもの。</summary>
    public string Label => Tool.Label;

    /// <summary>
    /// ホバー時に出す文言。補足があれば 2 行目に足す
    /// （何をするツールなのかは、名前だけでは伝わらないことがある）。
    /// </summary>
    public string ToolTip => Tool.Description is { Length: > 0 } description
        ? $"{Label}{Environment.NewLine}{description}"
        : Label;

    /// <summary>アイコンの図形。書かれていなければ null で、View 側が既定の印に戻す。</summary>
    public string? Icon => Tool.Icon;

    public ReactiveCommand<Unit, Unit> Command { get; }
}
