using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;

namespace MindMap.Abstractions.Viewers;

/// <summary>
/// 印刷する用紙。大きさはプリンターの設定から、余白はホストが決める。
/// </summary>
/// <param name="Size">用紙全体の大きさ（DIP、1/96 インチ）。</param>
/// <param name="Margin">四辺の余白（DIP）。</param>
/// <param name="Title">中身の名前。見出しやジョブ名に使える。</param>
public sealed record PrintPage(Size Size, Thickness Margin, string Title)
{
    /// <summary>余白を除いた、中身を置ける範囲。</summary>
    public Rect ContentArea => new(
        Margin.Left,
        Margin.Top,
        Math.Max(1, Size.Width - Margin.Left - Margin.Right),
        Math.Max(1, Size.Height - Margin.Top - Margin.Bottom));
}

/// <summary>
/// <see cref="IPrintableViewer"/> を書くときによく使う組み立て。
/// ホストとパッケージで同じ形になるよう、契約の側に置いてある。
/// </summary>
public static class PrintLayout
{
    /// <summary>
    /// 文書をページに分ける。渡した文書は用紙の大きさに合わせて書き換わるので、
    /// 画面に出しているものではなく、印刷用に作ったものを渡すこと。
    /// </summary>
    public static DocumentPaginator Paginate(FlowDocument document, PrintPage page)
    {
        document.PageWidth = page.Size.Width;
        document.PageHeight = page.Size.Height;
        document.PagePadding = page.Margin;

        // 既定のままだと、広い用紙では段組みになる。
        document.ColumnWidth = double.PositiveInfinity;

        // ページ分けの大きさは文書の PageWidth/PageHeight ではなくこちらで決まる。
        // 渡さないと既定の Letter（8.5×11 インチ）で割られ、A4 でも Letter の枚数になる。
        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.PageSize = page.Size;
        return paginator;
    }

    /// <summary>
    /// 要素を 1 ページに収める。縦横比は保ち、用紙より小さいものは引き伸ばさない。
    /// 要素は印刷用に作ったものを渡すこと（どこにも置かれていない必要がある）。
    /// </summary>
    /// <param name="naturalSize">要素の本来の大きさ（DIP）。分からなければ null（余白の内側いっぱいに置く）。</param>
    public static DocumentPaginator SinglePage(FrameworkElement element, PrintPage page, Size? naturalSize = null)
    {
        var area = page.ContentArea;
        var size = Fit(naturalSize, area.Size);

        element.Width = size.Width;
        element.Height = size.Height;

        var fixedPage = new FixedPage { Width = page.Size.Width, Height = page.Size.Height };
        FixedPage.SetLeft(element, area.Left + (area.Width - size.Width) / 2);
        FixedPage.SetTop(element, area.Top);
        fixedPage.Children.Add(element);

        fixedPage.Measure(page.Size);
        fixedPage.Arrange(new Rect(page.Size));
        fixedPage.UpdateLayout();

        var content = new PageContent();
        ((IAddChild)content).AddChild(fixedPage);

        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = page.Size;
        document.Pages.Add(content);

        return document.DocumentPaginator;
    }

    private static Size Fit(Size? natural, Size area)
    {
        if (natural is not { Width: > 0, Height: > 0 } n)
        {
            return area;
        }

        var scale = Math.Min(1, Math.Min(area.Width / n.Width, area.Height / n.Height));
        return new Size(n.Width * scale, n.Height * scale);
    }
}
