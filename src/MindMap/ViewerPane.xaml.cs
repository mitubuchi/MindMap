using System.Windows.Controls;
using System.Windows.Input;
using MindMap.ViewModels;

namespace MindMap;

public partial class ViewerPane : UserControl
{
    public ViewerPane()
    {
        InitializeComponent();
    }

    /// <summary>
    /// ウィンドウに登録したキー操作（Delete でノード削除、Tab で子ノード追加など）は、
    /// ビューアに入っている間に働くと事故になる。入力欄が処理し終えたあとの
    /// 上りの KeyDown で止め、ウィンドウまで届かないようにする。
    /// 入力欄が自分で処理したキー（文字の削除や Ctrl+Z など）はすでに Handled に
    /// なっていてここには来ないので、素通りしたものだけを捨てればよい。
    /// </summary>
    private void ViewerPane_KeyDown(object sender, KeyEventArgs e)
    {
        var control = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        var swallow = e.Key switch
        {
            Key.Delete or Key.Insert or Key.Tab or Key.Return or Key.F2 => true,

            // 入力欄の取り消しが尽きたあと、マップ側の取り消しに化けないようにする。
            Key.X or Key.C or Key.V or Key.A or Key.Z or Key.Y => control,

            _ => false,
        };

        if (swallow)
        {
            e.Handled = true;
        }
    }

    // 本文欄に入ってから抜けるまでを 1 回の Undo にまとめる。
    // 判断は ViewModel 側にあるので、ここでは出入りを伝えるだけにする。

    private void BodyEditor_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        (DataContext as ViewerViewModel)?.BeginEdit();

    private void BodyEditor_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        (DataContext as ViewerViewModel)?.EndEdit();
}
