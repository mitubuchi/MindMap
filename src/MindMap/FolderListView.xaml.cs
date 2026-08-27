using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MindMap.Services;
using MindMap.Services.Viewers;

namespace MindMap;

public partial class FolderListView : UserControl
{
    public FolderListView()
    {
        InitializeComponent();
    }

    /// <summary>行をダブルクリックしたら、関連付けられたアプリ（フォルダーならエクスプローラー）で開く。</summary>
    private void Row_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: FolderEntry entry })
        {
            Open(entry);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Enter でも開く。ここで受け止めておかないと、ウィンドウに登録した
    /// 「兄弟ノードを追加」に届いてしまう（ビューア側で止めてはいるが、
    /// 選んでいる行があるならそれを開くのが自然）。
    /// </summary>
    private void List_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Return || sender is not ListBox { SelectedItem: FolderEntry entry })
        {
            return;
        }

        Open(entry);
        e.Handled = true;
    }

    private void Open(FolderEntry entry) => ShellOpenService.Open(entry.Link, Window.GetWindow(this));
}
