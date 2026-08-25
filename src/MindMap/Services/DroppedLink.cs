using System.IO;
using System.Text;
using System.Windows;

namespace MindMap.Services;

/// <summary>
/// ドラッグ&ドロップされたデータからリンク文字列（URL かファイルパス）を取り出す。
/// ブラウザーやエクスプローラーが渡してくる形式はばらばらなので、扱える順に試す。
/// View から切り離してあるのは、この判定を単体で検証できるようにするため。
/// </summary>
public static class DroppedLink
{
    /// <summary>ブラウザーがリンクをドラッグしたときに使う形式（優先順）。</summary>
    private static readonly (string Format, Encoding Encoding)[] UrlFormats =
    [
        ("text/x-moz-url", Encoding.Unicode), // Firefox 系。"URL\nタイトル" の並び。
        ("UniformResourceLocatorW", Encoding.Unicode),
        ("UniformResourceLocator", Encoding.ASCII),
        ("text/uri-list", Encoding.UTF8),
        (DataFormats.UnicodeText, Encoding.Unicode),
        (DataFormats.Text, Encoding.UTF8),
    ];

    /// <summary>
    /// ブラウザーが渡してくる、URL とページの題名の組。
    /// 題名は渡されないこともある（アドレス欄からのドラッグなど）。
    /// </summary>
    public sealed record DroppedUrl(string Url, string? Title);

    /// <summary>ノードのリンクに使える種類。ここに無いものは落としても何も作らない。</summary>
    private static readonly HashSet<string> LinkSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeFtp, Uri.UriSchemeMailto,
    };

    /// <summary>
    /// ドロップされた URL をすべて返す。ファイルのドロップでは空を返す
    /// （ファイルは <see cref="ExtractFiles"/> が扱う）。
    ///
    /// 題名まで返すのは、ノードを新しく作るときに 1 行目に使うため。
    /// リンクを差し替えるだけの <see cref="Extract"/> は URL しか要らないので、そちらは触らない。
    /// </summary>
    public static IReadOnlyList<DroppedUrl> ExtractUrls(IDataObject data)
    {
        if (data.GetDataPresent(DataFormats.FileDrop))
        {
            return [];
        }

        foreach (var (format, encoding) in UrlFormats)
        {
            if (ReadLines(data, format, encoding) is not { Count: > 0 } lines)
            {
                continue;
            }

            // "URL\n題名" の繰り返しで渡す形式（text/x-moz-url）がある。
            // 2 行目が URL として読めなければ題名とみなす、という見分け方で両方に当たる。
            var urls = new List<DroppedUrl>();

            for (var i = 0; i < lines.Count; i++)
            {
                var url = lines[i];

                if (!IsLink(url))
                {
                    continue;
                }

                string? title = null;

                if (i + 1 < lines.Count && !IsLink(lines[i + 1]))
                {
                    title = lines[i + 1];
                    i++;
                }

                urls.Add(new DroppedUrl(url, title));
            }

            if (urls.Count > 0)
            {
                return urls;
            }
        }

        return [];
    }

    /// <summary>ノードのリンクとして扱える文字列か。</summary>
    private static bool IsLink(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) && LinkSchemes.Contains(uri.Scheme);

    /// <summary>
    /// ドロップされたファイル（フォルダーを含む）のパスをすべて返す。
    /// 1 つ目だけを使う <see cref="Extract"/> と違い、まとめてノード化する用。
    /// </summary>
    public static IReadOnlyList<string> ExtractFiles(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop) ||
            data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return [];
        }

        return files
            .Select(f => f?.Trim())
            .Where(f => !string.IsNullOrEmpty(f))
            .Select(f => f!)
            .ToList();
    }

    public static string? Extract(IDataObject data)
    {
        // ファイルのドロップは、そのフルパスをリンクにする。
        if (data.GetDataPresent(DataFormats.FileDrop) &&
            data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            var file = files[0]?.Trim();
            if (!string.IsNullOrEmpty(file))
            {
                return file;
            }
        }

        foreach (var (format, encoding) in UrlFormats)
        {
            if (ReadString(data, format, encoding) is { } value)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// 形式 1 つぶんを行に分けて読む。<see cref="ReadString"/> が先頭行だけを使うのに対し、
    /// こちらは題名や 2 つ目以降の URL も残す。
    /// </summary>
    private static IReadOnlyList<string>? ReadLines(IDataObject data, string format, Encoding encoding)
    {
        if (ReadRaw(data, format, encoding) is not { } text)
        {
            return null;
        }

        return text
            .Split('\n', '\r')
            .Select(line => line.Trim().Trim('\0').Trim())
            // text/uri-list は # で始まる行を注釈として使う。
            .Where(line => line.Length > 0 && line[0] != '#')
            .ToList();
    }

    /// <summary>形式 1 つぶんを文字列にして返す。渡され方（文字列 / ストリーム）を吸収する。</summary>
    private static string? ReadRaw(IDataObject data, string format, Encoding encoding)
    {
        if (!data.GetDataPresent(format))
        {
            return null;
        }

        var raw = data.GetData(format);
        var text = raw switch
        {
            string s => s,
            MemoryStream stream => encoding.GetString(stream.ToArray()),
            _ => raw?.ToString(),
        };

        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static string? ReadString(IDataObject data, string format, Encoding encoding)
    {
        if (ReadRaw(data, format, encoding) is not { } text)
        {
            return null;
        }

        // 改行区切りの先頭行だけを使う（URL の後にタイトルが続く形式があるため）。
        // 末尾のヌル文字（ワイド文字列由来）も落とす。
        text = text.Split('\n', '\r')[0].Trim().Trim('\0').Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
