using System.IO;

namespace MindMap.Services;

/// <summary>
/// ノードのリンクを、実際のファイルの場所に直す。
///
/// 相対パスは、リンク元のタブが置かれた場所を基準に解く。ビューアとノードのサムネイルが
/// 別々の解き方をすると、同じリンクなのに片方だけ出る、という食い違いが起きるので
/// 1 か所にまとめてある。
/// </summary>
public static class LinkPathResolver
{
    /// <param name="link">ノードに設定されたリンク。</param>
    /// <param name="baseFilePath">
    /// 相対パスの基準にするマップのファイル。まだ保存していないタブでは null で、
    /// その場合は相対パスを解けない（null が返る）。
    /// </param>
    public static string? Resolve(string? link, string? baseFilePath)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        var trimmed = link.Trim();

        try
        {
            // file:/// 形式で書かれたときだけ Uri 経由で直す。ふつうのパスまで通すと、
            // # などがフラグメントの記号として解釈されて途中で切れてしまう。
            if (trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
                uri.IsFile)
            {
                return uri.LocalPath;
            }

            if (Path.IsPathRooted(trimmed))
            {
                return Path.GetFullPath(trimmed);
            }

            if (baseFilePath is not { Length: > 0 } baseFile)
            {
                return null;
            }

            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(baseFile) ?? string.Empty, trimmed));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // パスに使えない文字が入っているなど。場所が決められなかったものとして扱う。
            return null;
        }
    }
}
