namespace MindMap.Abstractions.Settings;

/// <summary>
/// ホストの設定（config.txt）の写し。実行 1 回ぶんの値が
/// <see cref="Tools.MapToolContext.Settings"/> として渡る。
///
/// 名前と値の一覧をそのまま持つ。ホストが知っている項目だけに絞らないのは、
/// 設定ファイルには知らない項目も残る仕組みになっているためで、
/// パッケージが自分用の項目を書き足しておいて読むこともできる。
///
/// <b>読むだけ</b>で、書き戻す口は無い。設定を変えるのは利用者の操作（設定画面）だけにして、
/// パッケージを入れただけで設定が変わることが無いようにしている。
/// </summary>
public sealed class HostSettings
{
    /// <summary>何も設定されていない状態。</summary>
    public static HostSettings Empty { get; } = new()
    {
        Values = new Dictionary<string, string>(),
    };

    /// <summary>
    /// 名前と値。ホストが大文字小文字を区別しない辞書で渡す
    /// （設定ファイルは手で書き換えられるため）。
    /// </summary>
    public required IReadOnlyDictionary<string, string> Values { get; init; }

    /// <summary>
    /// 名前で引く値。書かれていない項目は空文字を返すので、
    /// 呼び出し側で「無いとき」を書き分けなくてよい。
    /// </summary>
    public string this[string key] => Values.TryGetValue(key, out var value) ? value : string.Empty;

    /// <summary>
    /// ノードのリンクを相対パスで書くときの基準にするフォルダー。未設定なら空。
    ///
    /// リンクの解決はホストが済ませて（<see cref="Viewers.ViewerContent.FilePath"/> は絶対パス）
    /// 渡すので、ふつうは要らない。ここに出しているのは、マップに置くファイルを
    /// 新しく作るツールが、置き場所を決めるために見たいことがあるため。
    /// </summary>
    public string RootPath => this[HostSettingKeys.RootPath];

    /// <summary>
    /// データの置き場。未設定なら空。
    ///
    /// ツールが作ったファイル（取り込んだ中身の書き出しなど）を置く場所として使う。
    /// 空のときは利用者がまだ決めていないので、<b>勝手に決めずに</b>
    /// 設定してもらう案内を出すこと。
    /// </summary>
    public string DataPath => this[HostSettingKeys.DataPath];
}

/// <summary>
/// 設定項目の名前。config.txt の中の見出しそのものなので、一度決めたら変えない。
///
/// ホスト側の設定一覧もこの定数を使う。名前を 2 か所に書くと、
/// 片方だけ直したときにパッケージから引けなくなるため。
/// </summary>
public static class HostSettingKeys
{
    /// <summary>ノードのリンクを相対パスで書くときの基準にするフォルダー。</summary>
    public const string RootPath = "Root Path";

    /// <summary>データの置き場。</summary>
    public const string DataPath = "Data Path";

    /// <summary>保存時に、Root Path の下のリンクを相対パスへ置き換えるか。</summary>
    public const string RootRelativeLinks = "Root Relative Links";
}
