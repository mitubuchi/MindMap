using System.Windows;
using MindMap.Abstractions.Settings;

namespace MindMap.Abstractions.Tools;

/// <summary>実行 1 回ぶんの前提。ホストが用意してツールに渡す。</summary>
public sealed class MapToolContext
{
    /// <summary>
    /// ツールが自前のダイアログを出すときの親ウィンドウ。
    /// <see cref="Window.Owner"/> に入れると、メインウィンドウの中央に出て、
    /// 閉じ忘れたまま裏に回ることもなくなる。
    /// </summary>
    public Window? Owner { get; init; }

    /// <summary>
    /// 進み具合の報告先。報告した文言がそのままステータスバーに出る。
    /// UI スレッドへの受け渡しはホスト側で済ませてあるので、別スレッドから報告してよい。
    /// </summary>
    public required IProgress<string> Progress { get; init; }

    /// <summary>
    /// ホストの設定（config.txt）の写し。書き出したファイルの置き場（
    /// <see cref="HostSettings.DataPath"/>）のように、利用者が決めた値を見たいとき用。
    ///
    /// 実行のたびに渡すので、設定画面で変えた値はその次の実行から効く。
    /// 覚え込まずに、毎回ここから読むこと。
    /// </summary>
    public required HostSettings Settings { get; init; }

    /// <summary>
    /// このツールが前に置いたノードの識別子（<see cref="MapNodeSpec.Key"/>）。
    /// 開いているマップに残っているぶんだけが入る。
    ///
    /// 前回の結果を踏まえて動きたいツール（前に見つけた相手だけをもう一度当たる、など）
    /// のために渡している。重ね方そのものはホストが面倒を見るので、
    /// 気にしないツールは見なくてよい。
    /// </summary>
    public required IReadOnlyCollection<string> ExistingKeys { get; init; }
}
