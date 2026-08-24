using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MindMap.Abstractions.Viewers;

namespace MindMap.Services.Viewers;

/// <summary>
/// 文字で中身を出す組み込みビューアの土台。何を出すかは派生が決める。
///
/// 読めなかった理由もこの枠の中に出す。ホスト側に「ビューアが失敗したとき」の
/// 分岐を作らずに済ませるため（<see cref="IContentViewer"/> の約束）。
/// </summary>
public abstract class TextContentViewer : IContentViewer
{
    private readonly Grid _root;
    private readonly TextBox _text;
    private readonly TextBlock _notice;

    protected TextContentViewer()
    {
        // 読み取り専用だが、選んでコピーはできるようにしておく。
        _text = new TextBox
        {
            IsReadOnly = true,
            IsReadOnlyCaretVisible = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0x3C, 0x46, 0x52)),
            FontFamily = new FontFamily("Consolas, Yu Gothic UI, Meiryo"),
            FontSize = 12,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        _notice = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x94, 0xA0)),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        _root = new Grid();
        _root.Children.Add(_text);
        _root.Children.Add(_notice);
    }

    public FrameworkElement View => _root;

    public async Task LoadAsync(ViewerContent content, CancellationToken cancellationToken)
    {
        // 読み込みの続きが UI スレッドに戻るかどうかは、呼ばれ方（同期コンテキストの有無）で
        // 変わる。画面を触るところだけは、戻り先を当てにせず明示的に渡す。
        var document = await BuildAsync(content, cancellationToken).ConfigureAwait(false);

        if (_root.Dispatcher.CheckAccess())
        {
            Apply(document);
            return;
        }

        await _root.Dispatcher.InvokeAsync(() => Apply(document));
    }

    private void Apply(TextDocument document)
    {
        if (document.Message is { } message)
        {
            Show(_notice, message);
            return;
        }

        Show(_text, document.Text ?? string.Empty);
    }

    /// <summary>中身を組み立てる。出せないときは理由を返す（例外は投げない）。</summary>
    protected abstract Task<TextDocument> BuildAsync(ViewerContent content, CancellationToken cancellationToken);

    public virtual void Dispose()
    {
        // 文字を出しているだけなので、手放すものはない。
    }

    private void Show(FrameworkElement target, string value)
    {
        _text.Visibility = ReferenceEquals(target, _text) ? Visibility.Visible : Visibility.Collapsed;
        _notice.Visibility = ReferenceEquals(target, _notice) ? Visibility.Visible : Visibility.Collapsed;

        if (target is TextBox box)
        {
            box.Text = value;

            // 前のファイルを読んだ位置が残らないよう、先頭に戻す。
            box.CaretIndex = 0;
            box.ScrollToHome();
        }
        else if (target is TextBlock block)
        {
            block.Text = value;
        }
    }
}
