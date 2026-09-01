using System.Windows;
using MindMap.Abstractions.Settings;
using MindMap.Abstractions.Tools;
using MindMap.Services.Settings;

namespace MindMap.Services.Tools;

/// <summary>
/// ホストから見たツール 1 つ。名乗り（名前・アイコン・ショートカット）はマニフェストから、
/// 中身は押されたときに読み込む DLL から来る。
///
/// ビューアと同じ考え方で遅らせている。ツールは押されるまで一度も使われないので、
/// 起動のたびに全パッケージの DLL を読む理由が無い。
/// </summary>
public sealed class PackageTool
{
    private readonly Func<IMapTool> _load;

    private IMapTool? _inner;

    public PackageTool(
        string id,
        string owner,
        string? nodeKeyField,
        string title,
        string? description,
        string? icon,
        string? shortcut,
        Func<IMapTool> load)
    {
        Id = id;
        Owner = owner;
        NodeKeyField = nodeKeyField;
        Title = title;
        Description = description;
        Icon = icon;
        Shortcut = shortcut;
        _load = load;
    }

    /// <summary>他と重ならない名前。"&lt;パッケージ ID&gt;/&lt;型名&gt;"。</summary>
    public string Id { get; }

    /// <summary>
    /// 提供元のパッケージ ID。ノードに書き込む識別子の名前空間になるので、
    /// 同じパッケージなら別のツールでも同じノードを見つけ直せる。
    /// </summary>
    public string Owner { get; }

    /// <summary>
    /// 識別子を置くノードの欄名。null なら Extensions の下（<see cref="NodeToolKey"/>）。
    /// 同じ形式のファイルを読み書きする別のプログラムに合わせたいときだけ、
    /// マニフェストで名前を指定する。
    /// </summary>
    public string? NodeKeyField { get; }

    /// <summary>ツールバーのツールチップに出す名前。</summary>
    public string Title { get; }

    /// <summary>名前だけでは足りないときの補足。ツールチップの 2 行目に出る。</summary>
    public string? Description { get; }

    /// <summary>アイコンの図形（Path のミニ言語）。読めない・書かれていない場合は既定の印を出す。</summary>
    public string? Icon { get; }

    /// <summary>"F5" のようなキー。書かれていなければショートカットは作らない。</summary>
    public string? Shortcut { get; }

    /// <summary>ツールバーに出す文言。ラベルとショートカットを 1 つにまとめたもの。</summary>
    public string Label => Shortcut is { Length: > 0 } key ? $"{Title} ({key})" : Title;

    /// <summary>
    /// 実行する。初回だけ DLL を読み込む。
    /// ダイアログの親はここで解決する（ツールを呼ぶ側にウィンドウを持ち回らせないため）。
    /// </summary>
    public Task<MapToolResult> RunAsync(
        IReadOnlyCollection<string> existingKeys,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var tool = _inner ??= _load();

        var context = new MapToolContext
        {
            Owner = Application.Current?.MainWindow,
            Progress = progress,
            ExistingKeys = existingKeys,

            // 実行のたびに写しを作る。設定画面で変えた値が、次の実行から効くようにするため。
            Settings = Snapshot(SettingsService.Current),
        };

        return tool.RunAsync(context, cancellationToken);
    }

    /// <summary>
    /// パッケージに渡す設定の写し。ホストが知らない項目も含めてそのまま渡す
    /// （パッケージが自分用の項目を config.txt に書いて読めるようにするため）。
    /// 設定ファイルは手で書き換えられるので、大文字小文字は区別しない。
    /// </summary>
    private static HostSettings Snapshot(AppSettings settings) => new()
    {
        Values = settings
            .ToEntries()
            .ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase),
    };
}
