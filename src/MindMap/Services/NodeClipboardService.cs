using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Windows;
using MindMap.Models;

namespace MindMap.Services;

/// <summary>
/// ノードの切り取り・コピー・貼り付けを Windows のクリップボード経由で行う。
/// タブ内だけで持ち回さずクリップボードを使うのは、別のタブはもちろん、
/// 別ウィンドウ・別プロセスの MindMap との間でも受け渡せるようにするため。
/// </summary>
public static class NodeClipboardService
{
    /// <summary>
    /// MindMap 専用のクリップボード形式。ファイル保存と同じ JSON を載せるので、
    /// 形式のバージョン管理は <see cref="MindMapDocument.Version"/> に任せられる。
    /// </summary>
    public const string ClipboardFormat = "MindMap.Nodes";

    /// <summary>他のアプリが握っている間はクリップボードを開けないので、少しだけ待って試し直す。</summary>
    private const int RetryCount = 5;

    private const int RetryDelayMilliseconds = 60;

    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    /// <summary>コピーしたノードのかたまりをクリップボードへ置く。</summary>
    public static void Write(MindMapDocument fragment)
    {
        var data = new DataObject();
        data.SetData(ClipboardFormat, JsonSerializer.Serialize(fragment, Options));

        // 他のアプリに貼れるよう、字下げ付きの箇条書きも一緒に載せる。
        data.SetText(ToOutline(fragment));

        // copy: true にしないと、MindMap を閉じた時点でクリップボードの中身が消えてしまう。
        Retry(() => Clipboard.SetDataObject(data, copy: true));
    }

    /// <summary>クリップボードから貼り付けられるノードを取り出す。無ければ null。</summary>
    public static MindMapDocument? Read()
    {
        return Retry(() =>
        {
            if (Clipboard.ContainsData(ClipboardFormat)
                && Clipboard.GetData(ClipboardFormat) is string json)
            {
                try
                {
                    var fragment = JsonSerializer.Deserialize<MindMapDocument>(json, Options);
                    if (fragment is { Nodes.Count: > 0 })
                    {
                        return fragment;
                    }
                }
                catch (JsonException)
                {
                    // 形式名は同じでも中身が壊れている場合。文字列として拾い直す。
                }
            }

            return Clipboard.ContainsText() ? FromText(Clipboard.GetText()) : null;
        });
    }

    /// <summary>
    /// MindMap 以外からコピーされた文字列を 1 つのノードにする。
    /// 1 行目をタイトル、残りを内容に入れる（URL をそのまま貼れるようにするため）。
    /// </summary>
    private static MindMapDocument? FromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var title = lines[0].Trim();
        var body = string.Join("\n", lines.Skip(1)).TrimEnd();

        return new MindMapDocument
        {
            Nodes =
            {
                new MindMapNodeDto
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    Body = body,
                },
            },
        };
    }

    /// <summary>他のアプリに貼るための文字列。親子の深さを字下げで表す。</summary>
    private static string ToOutline(MindMapDocument fragment)
    {
        var byParent = fragment.Nodes
            .Where(n => n.ParentId is not null)
            .ToLookup(n => n.ParentId!.Value);

        var ids = fragment.Nodes.Select(n => n.Id).ToHashSet();
        var builder = new StringBuilder();

        foreach (var root in fragment.Nodes.Where(n => n.ParentId is null || !ids.Contains(n.ParentId.Value)))
        {
            AppendOutline(builder, byParent, root, 0);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendOutline(
        StringBuilder builder,
        ILookup<Guid, MindMapNodeDto> byParent,
        MindMapNodeDto node,
        int depth)
    {
        var indent = new string(' ', depth * 2);
        builder.Append(indent).AppendLine(node.ResolveTitle());

        if (!string.IsNullOrWhiteSpace(node.Body))
        {
            foreach (var line in node.Body.Replace("\r\n", "\n").Split('\n'))
            {
                builder.Append(indent).Append("  ").AppendLine(line);
            }
        }

        foreach (var child in byParent[node.Id])
        {
            AppendOutline(builder, byParent, child, depth + 1);
        }
    }

    private static void Retry(Action action) => Retry<object?>(() =>
    {
        action();
        return null;
    });

    /// <summary>
    /// クリップボードは OS 全体で 1 つしかなく、他のアプリが開いている間は触れない。
    /// 失敗したまま例外を投げるとコピー操作そのものが落ちるので、少し待って試し直す。
    /// </summary>
    private static T? Retry<T>(Func<T?> action)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return action();
            }
            catch (ExternalException) when (attempt < RetryCount)
            {
                Thread.Sleep(RetryDelayMilliseconds);
            }
            catch (ExternalException)
            {
                return default;
            }
        }
    }
}
