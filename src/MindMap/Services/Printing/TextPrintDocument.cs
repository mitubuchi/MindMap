using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MindMap.Abstractions.Viewers;

namespace MindMap.Services.Printing;

/// <summary>
/// 文字だけの中身（ノードの本文、テキストファイル、アウトライン）を印刷用の文書にする。
/// 先頭に見出しを置き、本文は画面と同じ書体で、行ごとに折り返す。
/// </summary>
public static class TextPrintDocument
{
    private static readonly FontFamily BodyFont = new("Consolas, Yu Gothic UI, Meiryo");
    private static readonly FontFamily HeadingFont = new("Yu Gothic UI, Meiryo");
    private static readonly Brush RuleBrush = new SolidColorBrush(Color.FromRgb(0xC3, 0xCB, 0xD5));

    public static DocumentPaginator Create(string heading, string text, PrintPage page) =>
        PrintLayout.Paginate(Build(heading, text), page);

    /// <summary>見出しだけを置いた文書。表などを続けて足すときの土台にする。</summary>
    public static FlowDocument Begin(string heading)
    {
        var document = new FlowDocument
        {
            FontFamily = BodyFont,
            FontSize = 11,
            Foreground = Brushes.Black,
            Background = Brushes.White,
        };

        if (!string.IsNullOrWhiteSpace(heading))
        {
            document.Blocks.Add(new Paragraph(new Run(heading))
            {
                FontFamily = HeadingFont,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(0, 0, 0, 4),
                BorderBrush = RuleBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
            });
        }

        return document;
    }

    private static FlowDocument Build(string heading, string text)
    {
        var document = Begin(heading);

        // 1 段落にまとめて改行で区切る。行ごとに段落を作ると、空行や字下げの
        // 見た目が画面とずれるうえ、長い文書で段落が数千になる。
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        var lines = text.Replace("\r\n", "\n").Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }

            paragraph.Inlines.Add(new Run(lines[i]));
        }

        document.Blocks.Add(paragraph);
        return document;
    }
}
