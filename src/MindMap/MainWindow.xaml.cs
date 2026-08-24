using System.ComponentModel;
using System.Diagnostics;
using System.Reactive;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using MindMap.Services;
using MindMap.Services.Viewers;
using MindMap.ViewModels;
using ReactiveUI;

namespace MindMap;

public partial class MainWindow : Window, IViewFor<MainWindowViewModel>
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(MainWindowViewModel),
        typeof(MainWindow),
        new PropertyMetadata(null));

    /// <summary>ドラッグ中のノードと、掴んだ時点の位置。複数選択なら選択ぶんすべてが入る。</summary>
    private List<(NodeViewModel Node, double X, double Y)> _dragOrigins = new();

    private DocumentViewModel? _draggingDocument;
    private FrameworkElement? _draggingCanvas;
    private Point _dragStartPointerPosition;

    /// <summary>余白のドラッグによる範囲選択。</summary>
    private DocumentViewModel? _bandDocument;

    private FrameworkElement? _bandCanvas;
    private Shape? _bandRectangle;
    private Point _bandStart;
    private bool _bandAdditive;

    private bool _isPanning;
    private Point _panStartPointerPosition;
    private Point _panStartOffset;

    /// <summary>スクロール位置を復元している間は、その動きを記録し返さないようにする。</summary>
    private bool _restoringScroll;

    /// <summary>ビューアの幅を変えている最中かどうかと、掴んだ時点の幅・ポインタの位置。</summary>
    private bool _resizingViewer;

    private double _resizeStartWidth;
    private Point _resizeStartPointerPosition;

    /// <summary>未保存確認のために閉じるのを一度キャンセルするので、二周目を見分けるフラグ。</summary>
    private bool _closeConfirmed;

    public MainWindow(ViewerRegistry viewers)
    {
        InitializeComponent();

        ViewModel = new MainWindowViewModel(viewers);
        DataContext = ViewModel;

        RegisterInteractionHandlers();
    }

    public MainWindowViewModel? ViewModel
    {
        get => (MainWindowViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = (MainWindowViewModel?)value;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_closeConfirmed || ViewModel is null)
        {
            base.OnClosing(e);
            return;
        }

        // 確認ダイアログを挟むためにいったん閉じるのを取り消し、答えが出てから閉じ直す。
        e.Cancel = true;
        base.OnClosing(e);
        _ = ConfirmAndCloseAsync();
    }

    private async Task ConfirmAndCloseAsync()
    {
        // OnClosing の処理中に Close() を呼ぶと WPF が例外を投げるので、抜けきるまで待つ。
        await Dispatcher.Yield(DispatcherPriority.Background);

        if (ViewModel is not null && await ViewModel.CanCloseAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }

    /// <summary>ViewModel からのダイアログ要求を、実際の WPF ダイアログにつなぐ。</summary>
    private void RegisterInteractionHandlers()
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.ShowOpenFileDialog.RegisterHandler(context =>
        {
            var dialog = new OpenFileDialog
            {
                Filter = MindMapFileService.FileDialogFilter,
                DefaultExt = MindMapFileService.FileExtension,
            };

            context.SetOutput(dialog.ShowDialog(this) == true ? dialog.FileName : null);
        });

        ViewModel.ShowSaveFileDialog.RegisterHandler(context =>
        {
            var dialog = new SaveFileDialog
            {
                Filter = MindMapFileService.FileDialogFilter,
                DefaultExt = MindMapFileService.FileExtension,
                AddExtension = true,
                FileName = context.Input ?? string.Empty,
            };

            context.SetOutput(dialog.ShowDialog(this) == true ? dialog.FileName : null);
        });

        ViewModel.ShowLinkFileDialog.RegisterHandler(context =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "リンク先のファイルを選択",
                Filter = "すべてのファイル (*.*)|*.*",
                CheckFileExists = true,
            };

            context.SetOutput(dialog.ShowDialog(this) == true ? dialog.FileName : null);
        });

        ViewModel.ConfirmSaveChanges.RegisterHandler(context =>
        {
            var result = MessageBox.Show(
                this,
                $"「{context.Input}」の変更が保存されていません。\n保存しますか？",
                "MindMap",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            context.SetOutput(result switch
            {
                MessageBoxResult.Yes => SaveChangesResult.Save,
                MessageBoxResult.No => SaveChangesResult.Discard,
                _ => SaveChangesResult.Cancel,
            });
        });

        ViewModel.ShowError.RegisterHandler(context =>
        {
            MessageBox.Show(this, context.Input, "MindMap", MessageBoxButton.OK, MessageBoxImage.Error);
            context.SetOutput(Unit.Default);
        });

        ViewModel.OpenExternal.RegisterHandler(context =>
        {
            OpenWithShell(context.Input);
            context.SetOutput(Unit.Default);
        });
    }

    /// <summary>関連付けが無いファイルを開こうとしたときに Windows が返すコード。</summary>
    private const int NoAssociationErrorCode = 1155;

    /// <summary>
    /// URL やファイルを OS に渡す。URL は既定のブラウザーで、ファイルは関連付けられたアプリで開く。
    /// </summary>
    private void OpenWithShell(string target)
    {
        try
        {
            // UseShellExecute にすると、実行ではなく「開く」の既定の動作に委ねられる。
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == NoAssociationErrorCode)
        {
            // 開くアプリが決まっていないので、Windows の「プログラムから開く」を出す。
            Process.Start(new ProcessStartInfo("rundll32.exe", $"shell32.dll,OpenAs_RunDLL {target}")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"リンクを開けませんでした。\n\n{target}\n\n{ex.Message}",
                "MindMap",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    // ------------------------------------------------------------ ノードの操作

    /// <summary>
    /// ノードの実寸を ViewModel に返す。接続線の端点とドラッグの移動範囲が、
    /// 中身の量で変わる実際の大きさを知る必要があるため。
    /// </summary>
    private void Node_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NodeViewModel node })
        {
            node.Width = e.NewSize.Width;
            node.Height = e.NewSize.Height;
        }
    }

    private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NodeViewModel node } element
            || FindDocument(element) is not { } document)
        {
            return;
        }

        e.Handled = true;

        // Ctrl / Shift 付きのクリックは選択の足し引きだけ。移動も編集も始めない。
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            document.ToggleSelection(node);
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            document.AddToSelection(node);
            return;
        }

        if (e.ClickCount >= 2)
        {
            document.SelectedNode = node;
            node.IsEditing = true;
            return;
        }

        // 複数選択のうちの 1 つを掴んだときは選択を崩さない（まとめて動かすため）。
        if (!document.SelectedNodes.Contains(node))
        {
            document.SelectedNode = node;
        }

        // 編集中のノードはドラッグせず、クリックはカーソル移動に任せる。
        if (node.IsEditing)
        {
            return;
        }

        // キャンバスはタブの中身のテンプレートにあるので、名前ではなく visual tree から辿る。
        if (FindNamedAncestor(element, "CanvasRoot") is not { } canvas)
        {
            return;
        }

        var moving = document.SelectedNodes.Contains(node)
            ? document.SelectedNodes.ToList()
            : new List<NodeViewModel> { node };

        _dragOrigins = moving.Select(n => (Node: n, n.X, n.Y)).ToList();
        _draggingDocument = document;
        _draggingCanvas = canvas;
        _dragStartPointerPosition = e.GetPosition(canvas);
        element.CaptureMouse();
    }

    private void Node_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragOrigins.Count == 0 || _draggingCanvas is not { } canvas
            || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(canvas);
        var deltaX = current.X - _dragStartPointerPosition.X;
        var deltaY = current.Y - _dragStartPointerPosition.Y;

        // キャンバスの外にノードが出て行方不明にならないよう内側に留める。
        // 複数を動かすときは、位置関係が崩れないよう移動量そのものを制限する。
        var minX = _dragOrigins.Max(o => -o.X);
        var maxX = _dragOrigins.Min(o => Math.Max(0, canvas.Width - o.Node.Width) - o.X);
        var minY = _dragOrigins.Max(o => -o.Y);
        var maxY = _dragOrigins.Min(o => Math.Max(0, canvas.Height - o.Node.Height) - o.Y);

        deltaX = Math.Clamp(deltaX, minX, Math.Max(minX, maxX));
        deltaY = Math.Clamp(deltaY, minY, Math.Max(minY, maxY));

        foreach (var (node, originX, originY) in _dragOrigins)
        {
            node.X = originX + deltaX;
            node.Y = originY + deltaY;
        }
    }

    private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { IsMouseCaptured: true } element)
        {
            element.ReleaseMouseCapture();
        }

        if (_dragOrigins.Count > 0 && _draggingDocument is { } document)
        {
            // ドラッグ全体を 1 回の Undo にまとめる（動いていなければ何も積まれない）。
            document.CompleteNodeDrag(_dragOrigins);

            // 複数選択のノードを動かさずに離したら、そのノードだけの選択に絞る。
            // 押した時点で絞ってしまうと、まとめて動かすドラッグが始められない。
            var moved = _dragOrigins.Any(o => o.Node.X != o.X || o.Node.Y != o.Y);
            if (!moved && _dragOrigins.Count > 1 && sender is FrameworkElement { DataContext: NodeViewModel node })
            {
                document.SelectedNode = node;
            }
        }

        _dragOrigins = new();
        _draggingDocument = null;
        _draggingCanvas = null;
    }

    // ------------------------------------------------------------ 範囲選択

    /// <summary>余白を押したところから、囲んだノードをまとめて選ぶドラッグを始める。</summary>
    private void CanvasRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DocumentViewModel document } canvas)
        {
            return;
        }

        // Ctrl / Shift を押していれば今の選択に足す。押していなければ、余白のクリックで選択解除。
        _bandAdditive = (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;
        if (!_bandAdditive)
        {
            document.ClearSelection();
        }

        _bandDocument = document;
        _bandCanvas = canvas;
        _bandStart = e.GetPosition(canvas);
        _bandRectangle = FindNamedDescendant(canvas, "SelectionBox") as Shape;
        canvas.CaptureMouse();

        Keyboard.ClearFocus();
    }

    private void CanvasRoot_MouseMove(object sender, MouseEventArgs e)
    {
        if (_bandCanvas is not { } canvas || _bandRectangle is not { } box
            || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var band = BandRect(e.GetPosition(canvas));

        Canvas.SetLeft(box, band.X);
        Canvas.SetTop(box, band.Y);
        box.Width = band.Width;
        box.Height = band.Height;
        box.Visibility = Visibility.Visible;
    }

    private void CanvasRoot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_bandCanvas is not { } canvas)
        {
            return;
        }

        canvas.ReleaseMouseCapture();

        if (_bandRectangle is { } box)
        {
            box.Visibility = Visibility.Collapsed;
        }

        // 枠を出さないまま離した（ただのクリック）ときは選択を変えない。
        var band = BandRect(e.GetPosition(canvas));
        if (_bandDocument is { } document && band is { Width: >= 3 } or { Height: >= 3 })
        {
            var hits = document.Nodes
                .Where(n => band.IntersectsWith(new Rect(n.X, n.Y, n.Width, n.Height)))
                .ToList();

            document.SelectNodes(hits, _bandAdditive);
        }

        _bandDocument = null;
        _bandCanvas = null;
        _bandRectangle = null;
    }

    private Rect BandRect(Point current) => new(
        Math.Min(_bandStart.X, current.X),
        Math.Min(_bandStart.Y, current.Y),
        Math.Abs(current.X - _bandStart.X),
        Math.Abs(current.Y - _bandStart.Y));

    // ------------------------------------------------------------ ノードのリンク

    /// <summary>URL やファイルをノードにドロップできるか判定し、カーソルの見た目を切り替える。</summary>
    private void Node_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DroppedLink.Extract(e.Data) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>ブラウザーからの URL や、エクスプローラーからのファイルをリンクとして受け取る。</summary>
    private void Node_Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NodeViewModel node } element
            || FindDocument(element) is not { } document)
        {
            return;
        }

        if (DroppedLink.Extract(e.Data) is { } link)
        {
            document.SetLink(node, link);
            document.SelectedNode = node;
        }

        e.Handled = true;
    }

    /// <summary>キャンバスの余白にファイルを落とせるか判定する。</summary>
    private void CanvasRoot_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DroppedLink.ExtractFiles(e.Data).Count > 0
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// エクスプローラーから落とされたファイルを、その場所にノードとして作る。
    /// ノードの上に落としたときは <see cref="Node_Drop"/> がリンク設定として先に処理する。
    /// </summary>
    private void CanvasRoot_Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DocumentViewModel document } canvas)
        {
            return;
        }

        var files = DroppedLink.ExtractFiles(e.Data);
        if (files.Count == 0)
        {
            return;
        }

        // 落とした位置に置く。キャンバスの外にはみ出して行方不明にならないよう内側に留める。
        var point = e.GetPosition(canvas);
        var x = Math.Clamp(point.X, 0, Math.Max(0, canvas.Width - NodeViewModel.DefaultWidth));
        var y = Math.Clamp(point.Y, 0, Math.Max(0, canvas.Height - NodeViewModel.DefaultHeight));

        document.AddFileNodes(files, x, y);
        e.Handled = true;
    }

    /// <summary>
    /// 右クリックでそのノードを選んでおく（コンテキストメニューの対象を確定させる）。
    /// すでに複数選択に入っているノードなら、選択を崩さずそのまま対象にする。
    /// </summary>
    private void Node_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NodeViewModel node } element
            && FindDocument(element) is { } document
            && !document.SelectedNodes.Contains(node))
        {
            document.SelectedNode = node;
        }
    }

    private async void ContextExtractChildren_Click(object sender, RoutedEventArgs e)
    {
        if (NodeOf(sender) is { } node && DocumentOf(sender) is { } document)
        {
            await document.ExtractChildrenToFileAsync(node);
        }
    }

    private void ContextOpenLink_Click(object sender, RoutedEventArgs e)
    {
        if (NodeOf(sender) is { } node)
        {
            ViewModel?.OpenLinkCommand.Execute(node).Subscribe();
        }
    }

    private async void ContextSetFileLink_Click(object sender, RoutedEventArgs e)
    {
        if (NodeOf(sender) is { } node && DocumentOf(sender) is { } document)
        {
            await document.SetFileLinkAsync(node);
        }
    }

    private void ContextRemoveLink_Click(object sender, RoutedEventArgs e)
    {
        if (NodeOf(sender) is { } node && DocumentOf(sender) is { } document)
        {
            document.SetLink(node, string.Empty);
        }
    }

    /// <summary>コンテキストメニューの項目から、対象のノードを取り出す。</summary>
    private static NodeViewModel? NodeOf(object sender) =>
        (sender as MenuItem)?.DataContext as NodeViewModel;

    /// <summary>コンテキストメニューの項目から、そのノードが属するドキュメントを取り出す。</summary>
    private DocumentViewModel? DocumentOf(object sender)
    {
        // ContextMenu は visual tree の外にあるので、開く元になった Border から辿る。
        if (sender is MenuItem { Parent: ContextMenu { PlacementTarget: { } target } })
        {
            return FindDocument(target);
        }

        return null;
    }

    // ------------------------------------------------------------ ノードの編集

    /// <summary>
    /// 編集開始時にタイトル欄へフォーカスを移す。Loaded ではなく可視状態の変化を見るのは、
    /// テキストボックスが非表示のまま最初から存在していて Loaded が一度しか起きないため。
    /// </summary>
    private void TitleBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox textBox || e.NewValue is not true)
        {
            return;
        }

        // レイアウトが済む前は Focus() が効かないので一拍置く。
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                textBox.Focus();
                textBox.SelectAll();
            }),
            DispatcherPriority.Input);
    }

    /// <summary>タイトル欄と内容欄で共通のキー操作。振る舞いの違いは AcceptsReturn から導く。</summary>
    private void EditBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: NodeViewModel node } textBox)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                FindDocument(textBox)?.CancelTextEdit();
                Keyboard.ClearFocus();
                e.Handled = true;
                break;

            // Ctrl+Enter はどちらの欄からでも編集を確定する。
            case Key.Enter when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                node.IsEditing = false;
                Keyboard.ClearFocus();
                e.Handled = true;
                break;

            // タイトルは 1 行だけなので、Enter は改行ではなく内容欄への移動にあてる。
            // 内容欄（AcceptsReturn=True）ではそのまま改行として通す。
            case Key.Enter when !textBox.AcceptsReturn:
                textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// 編集の終了判定。タイトル欄と内容欄の行き来ではフォーカスが箱の中に留まるので、
    /// 個々の LostFocus ではなくパネル全体から抜けたときだけ確定する。
    /// </summary>
    private void EditPanel_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false && sender is FrameworkElement { DataContext: NodeViewModel node })
        {
            node.IsEditing = false;
        }
    }

    // ------------------------------------------------------------ スクロールとズーム

    /// <summary>タブを切り替えたとき、そのドキュメントの表示位置に戻す。</summary>
    private void Scroller_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not ScrollViewer scroller || e.NewValue is not DocumentViewModel document)
        {
            return;
        }

        _restoringScroll = true;

        // 新しい中身のレイアウトが終わるまではスクロールできない。
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                scroller.ScrollToHorizontalOffset(document.ScrollOffsetX);
                scroller.ScrollToVerticalOffset(document.ScrollOffsetY);
                _restoringScroll = false;
            }),
            DispatcherPriority.Loaded);
    }

    private void Scroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_restoringScroll)
        {
            return;
        }

        if (sender is ScrollViewer { DataContext: DocumentViewModel document } scroller)
        {
            document.ScrollOffsetX = scroller.HorizontalOffset;
            document.ScrollOffsetY = scroller.VerticalOffset;
        }
    }

    /// <summary>Ctrl + ホイールで拡大縮小。カーソルの下にある位置が動かないようスクロール位置も補正する。</summary>
    private void Scroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer { DataContext: DocumentViewModel document, Content: FrameworkElement canvas } scroller
            || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        var canvasPoint = e.GetPosition(canvas);
        var viewportPoint = e.GetPosition(scroller);

        document.Zoom *= e.Delta > 0 ? 1.1 : 1 / 1.1;

        // 新しい拡大率でのレイアウトが確定してからでないとスクロール位置を計算できない。
        scroller.UpdateLayout();
        scroller.ScrollToHorizontalOffset(canvasPoint.X * document.Zoom - viewportPoint.X);
        scroller.ScrollToVerticalOffset(canvasPoint.Y * document.Zoom - viewportPoint.Y);

        e.Handled = true;
    }

    private void Scroller_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || sender is not ScrollViewer scroller)
        {
            return;
        }

        _isPanning = true;
        _panStartPointerPosition = e.GetPosition(scroller);
        _panStartOffset = new Point(scroller.HorizontalOffset, scroller.VerticalOffset);
        scroller.CaptureMouse();
        scroller.Cursor = Cursors.ScrollAll;
        e.Handled = true;
    }

    private void Scroller_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning || sender is not ScrollViewer scroller)
        {
            return;
        }

        var current = e.GetPosition(scroller);
        scroller.ScrollToHorizontalOffset(_panStartOffset.X - (current.X - _panStartPointerPosition.X));
        scroller.ScrollToVerticalOffset(_panStartOffset.Y - (current.Y - _panStartPointerPosition.Y));
    }

    private void Scroller_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || !_isPanning || sender is not ScrollViewer scroller)
        {
            return;
        }

        _isPanning = false;
        scroller.ReleaseMouseCapture();
        scroller.Cursor = Cursors.Arrow;
        e.Handled = true;
    }

    // ------------------------------------------------------------ ビューアの幅

    /// <summary>
    /// ビューアとキャンバスの間の帯をドラッグして幅を変える。
    /// GridSplitter を使わないのは、DockPanel のままで済ませるため。
    /// 掴んだ時点を基準に測るので、上限に当たっても位置がずれない。
    /// </summary>
    private void ViewerSplitter_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is null || sender is not UIElement splitter)
        {
            return;
        }

        _resizingViewer = true;
        _resizeStartWidth = ViewModel.Viewer.Width;
        _resizeStartPointerPosition = e.GetPosition(this);
        splitter.CaptureMouse();
        e.Handled = true;
    }

    private void ViewerSplitter_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_resizingViewer || ViewModel is null)
        {
            return;
        }

        // 左へ動かすほど広がる。行き過ぎてキャンバスが潰れないよう、幅は ViewModel 側で丸める。
        var delta = _resizeStartPointerPosition.X - e.GetPosition(this).X;
        ViewModel.Viewer.Resize(_resizeStartWidth + delta, ActualWidth);
    }

    private void ViewerSplitter_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_resizingViewer || sender is not UIElement splitter)
        {
            return;
        }

        _resizingViewer = false;
        splitter.ReleaseMouseCapture();
        e.Handled = true;
    }

    // ------------------------------------------------------------ visual tree の探索

    /// <summary>
    /// この要素が属しているタブのドキュメントを探す。ノードは DataTemplate の中にいるので、
    /// ウィンドウの ViewModel からではなく自分の位置から辿る必要がある。
    /// </summary>
    private static DocumentViewModel? FindDocument(DependencyObject start)
    {
        for (var current = start; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { DataContext: DocumentViewModel document })
            {
                return document;
            }
        }

        return null;
    }

    /// <summary>
    /// テンプレートの中にある要素を名前で探す。DataTemplate 内の x:Name は
    /// ウィンドウのフィールドにならないので、visual tree を下って見つける。
    /// </summary>
    private static FrameworkElement? FindNamedDescendant(DependencyObject start, string name)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(start); i++)
        {
            var child = VisualTreeHelper.GetChild(start, i);

            if (child is FrameworkElement element && element.Name == name)
            {
                return element;
            }

            if (FindNamedDescendant(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static FrameworkElement? FindNamedAncestor(DependencyObject start, string name)
    {
        for (var current = start; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement element && element.Name == name)
            {
                return element;
            }
        }

        return null;
    }
}
