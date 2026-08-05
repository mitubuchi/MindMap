using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MindMap.Services;

/// <summary>
/// リンク先の種類を見分け、ファイルなら関連付けられたアプリの小さなアイコンを取り出す。
/// マインドマップ・Web・メール・フォルダーは種類だけを返し、アイコンは View 側の
/// 線画（<c>Icon.MindMap</c> など）に任せる。見た目をアプリ全体で揃えるため。
/// </summary>
public static class ShellIconService
{
    /// <summary>
    /// 拡張子ごとに 1 回だけ引く。ノードの数だけシェルに問い合わせると重く、
    /// 関連付けはアプリの起動中に変わることも稀なため。
    /// </summary>
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>リンク先の種類を見分ける。</summary>
    public static LinkKind Classify(string? link) =>
        string.IsNullOrWhiteSpace(link) ? LinkKind.Unknown : Resolve(link.Trim()).Kind;

    /// <summary>
    /// リンク先のファイルに関連付けられたアプリのアイコン。
    /// ファイル以外（Web・フォルダーなど）や、引けなかったときは null を返す。
    /// </summary>
    public static ImageSource? ForLink(string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        if (Resolve(link.Trim()) is not { Kind: LinkKind.File, Key: { } key })
        {
            return null;
        }

        if (!Cache.TryGetValue(key, out var icon))
        {
            icon = Load(key);
            Cache[key] = icon;
        }

        return icon;
    }

    /// <summary>
    /// リンクを種類に分ける。<c>Key</c> はアイコンを引くときの単位（拡張子、または
    /// ファイルごとに固有のアイコンを持つものはそのパス）で、種類が File のときだけ入る。
    /// </summary>
    private static (LinkKind Kind, string? Key) Resolve(string link)
    {
        var path = link;

        if (Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            if (!uri.IsFile)
            {
                return uri.Scheme switch
                {
                    "http" or "https" => (LinkKind.Web, null),
                    "mailto" => (LinkKind.Mail, null),

                    // 独自のスキームはどう開かれるか分からないので、既定のリンク記号に任せる。
                    _ => (LinkKind.Unknown, null),
                };
            }

            path = uri.LocalPath;
        }

        try
        {
            // 絶対パスのときだけ実物を見る。相対パスは基準の場所が分からないので、
            // 現在の作業フォルダーを基準に誤判定しないよう、名前だけで決める。
            if (Path.IsPathRooted(path))
            {
                if (Directory.Exists(path))
                {
                    return (LinkKind.Folder, null);
                }

                // 実行ファイルやショートカットは 1 つずつ違うアイコンを持つので、実物から引く。
                if (File.Exists(path) && HasOwnIcon(path))
                {
                    return (LinkKind.File, path);
                }
            }

            var extension = Path.GetExtension(path);

            // 拡張子が無いものはフォルダーとみなす。
            if (string.IsNullOrEmpty(extension))
            {
                return (LinkKind.Folder, null);
            }

            return string.Equals(extension, MindMapFileService.FileExtension, StringComparison.OrdinalIgnoreCase)
                ? (LinkKind.MindMap, null)
                : (LinkKind.File, extension);
        }
        catch (ArgumentException)
        {
            // パスに使えない文字が入っていた場合。種類は決められない。
            return (LinkKind.Unknown, null);
        }
    }

    /// <summary>拡張子では代表させられない、ファイルごとに固有のアイコンを持つ種類か。</summary>
    private static bool HasOwnIcon(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".exe" or ".lnk" or ".ico" or ".url" or ".msc" or ".cpl";

    private static ImageSource? Load(string key)
    {
        var info = default(ShFileInfo);

        // 拡張子だけを渡すときは USEFILEATTRIBUTES を付ける。実在しないパスでも
        // 「その拡張子のアイコン」を引けて、ディスクにも触らないので速い。
        var useAttributes = !Path.IsPathRooted(key);
        var flags = ShgfiIcon | ShgfiLargeIcon | (useAttributes ? ShgfiUseFileAttributes : 0);

        if (SHGetFileInfo(key, FileAttributeNormal, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), flags) == 0
            || info.hIcon == nint.Zero)
        {
            return null;
        }

        return ToImageSource(info.hIcon);
    }

    /// <summary>
    /// アイコンのハンドルを WPF の画像にする。ハンドルは OS の資源なので、
    /// 画像を作ったらその場で返す（画像側は自前のコピーを持つ）。
    /// </summary>
    private static ImageSource? ToImageSource(nint hIcon)
    {
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            // 別のスレッドから触られても安全なように、そして描画を軽くするために凍らせる。
            source.Freeze();
            return source;
        }
        catch (Exception)
        {
            // 壊れたアイコンを持つファイルもあるので、その場合は既定のリンク記号に任せる。
            return null;
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    // ------------------------------------------------------------ Win32

    private const uint FileAttributeNormal = 0x80;

    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiUseFileAttributes = 0x000000010;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public nint hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref ShFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint hIcon);
}
