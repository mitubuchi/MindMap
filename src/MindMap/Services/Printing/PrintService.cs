using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using MindMap.Abstractions.Viewers;

namespace MindMap.Services.Printing;

/// <summary>
/// 印刷のダイアログを出して、組み立てたものをプリンターに送る。
///
/// 何をどう組むかは呼ぶ側（ビューア）が決め、ここは用紙の大きさと余白を
/// 渡すだけにする。中身の種類ごとの事情をここに持ち込まないため。
/// </summary>
public static class PrintService
{
    /// <summary>四辺の余白。15 mm。家庭用のプリンターの印字できない縁より十分広く取る。</summary>
    private const double MarginDip = 15 / 25.4 * 96;

    /// <param name="title">中身の名前。印刷キューに出るジョブ名になる。</param>
    /// <param name="build">用紙に合わせて組み立てる。出せるものが無ければ null。</param>
    /// <param name="owner">ダイアログとエラーの親ウィンドウ。</param>
    public static void Print(string title, Func<PrintPage, DocumentPaginator?> build, Window? owner)
    {
        var dialog = new PrintDialog();

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var page = new PrintPage(PageSize(dialog), new Thickness(MarginDip), title);

            if (build(page) is not { } paginator)
            {
                Show(owner, "印刷できるものがありません。", MessageBoxImage.Information);
                return;
            }

            dialog.PrintDocument(paginator, string.IsNullOrWhiteSpace(title) ? "MindMap" : title);
        }
        catch (PrintingCanceledException)
        {
            // 利用者がプリンター側で取り消した。知らせることは無い。
        }
        catch (Exception ex)
        {
            Show(owner, $"印刷できませんでした。\n\n{ex.Message}", MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 用紙の大きさ。プリンターの向きの設定も反映した値を使う。
    /// PrintableAreaWidth/Height はダイアログで選んだ用紙と向きに合わせて入っている。
    /// </summary>
    private static Size PageSize(PrintDialog dialog) =>
        new(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);

    private static void Show(Window? owner, string message, MessageBoxImage image)
    {
        if (owner is not null)
        {
            MessageBox.Show(owner, message, "MindMap", MessageBoxButton.OK, image);
            return;
        }

        MessageBox.Show(message, "MindMap", MessageBoxButton.OK, image);
    }
}
