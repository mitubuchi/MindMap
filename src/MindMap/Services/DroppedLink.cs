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

    private static string? ReadString(IDataObject data, string format, Encoding encoding)
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

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        // 改行区切りの先頭行だけを使う（URL の後にタイトルが続く形式があるため）。
        // 末尾のヌル文字（ワイド文字列由来）も落とす。
        text = text.Split('\n', '\r')[0].Trim().Trim('\0').Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
