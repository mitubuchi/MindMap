using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using MindMap.Models;
using MindMap.Services;
using MindMap.Undo;
using ReactiveUI;

namespace MindMap.ViewModels;

/// <summary>
/// タブ 1 枚ぶん、つまりマインドマップ 1 つ分の状態と操作。
/// 保存先・編集履歴・拡大率までドキュメントごとに独立して持つ。
/// </summary>
public sealed class DocumentViewModel : ReactiveObject
{
    private const double HorizontalGap = 72;
    private const double VerticalGap = 24;

    public const double MinZoom = 0.3;
    public const double MaxZoom = 3.0;
    private const double ZoomStep = 1.2;

    /// <summary>ノードごとの購読（未保存フラグと編集セッションの監視）。</summary>
    private readonly Dictionary<Guid, IDisposable> _nodeSubscriptions = new();

    private readonly UndoStack _history = new();

    private readonly Interaction<string?, string?> _showSaveFileDialog;
    private readonly Interaction<Unit, string?> _showLinkFileDialog;
    private readonly Interaction<string, SaveChangesResult> _confirmSaveChanges;
    private readonly Interaction<string, Unit> _showError;

    /// <summary>まだ保存していないドキュメントのタブ名。</summary>
    private readonly string _untitledName;

    private readonly ObservableAsPropertyHelper<string> _displayName;

    /// <summary>編集中のノードと、編集を始めた時点の内容。Undo と Escape の取り消しに使う。</summary>
    private (NodeViewModel Node, string Title, string Body, string Link)? _activeEdit;

    private NodeViewModel? _selectedNode;
    private string? _currentFilePath;
    private bool _isDirty;
    private bool _canUndo;
    private bool _canRedo;
    private double _zoom = 1.0;

    public DocumentViewModel(
        string untitledName,
        Interaction<string?, string?> showSaveFileDialog,
        Interaction<Unit, string?> showLinkFileDialog,
        Interaction<string, SaveChangesResult> confirmSaveChanges,
        Interaction<string, Unit> showError)
    {
        _untitledName = untitledName;
        _showSaveFileDialog = showSaveFileDialog;
        _showLinkFileDialog = showLinkFileDialog;
        _confirmSaveChanges = confirmSaveChanges;
        _showError = showError;

        var hasSelection = this.WhenAnyValue(x => x.SelectedNode).Select(node => node is not null);

        // Tab / Enter / Delete はテキスト編集中も押されるので、編集中は無効にして
        // キー入力をテキストボックスに素通しさせる。
        var canEditStructure = this.WhenAnyValue(
            x => x.SelectedNode,
            x => x.SelectedNode!.IsEditing,
            (node, isEditing) => node is not null && !isEditing);

        // ルートを消すとマップが空になってしまうので、そもそも実行できないようにする
        // （押せるボタンを押させてからエラーで断るより、無効にして見せた方が分かりやすい）。
        var canDelete = this.WhenAnyValue(
            x => x.SelectedNode,
            x => x.SelectedNode!.IsEditing,
            (node, isEditing) => node is { Parent: not null } && !isEditing);

        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
        SaveAsCommand = ReactiveCommand.CreateFromTask(SaveAsAsync);
        AddChildCommand = ReactiveCommand.Create(AddChild, canEditStructure);
        AddSiblingCommand = ReactiveCommand.Create(AddSibling, canEditStructure);
        DeleteNodeCommand = ReactiveCommand.Create(DeleteSelectedNode, canDelete);
        BeginEditCommand = ReactiveCommand.Create(BeginEditSelectedNode, hasSelection);
        UndoCommand = ReactiveCommand.Create(_history.Undo, this.WhenAnyValue(x => x.CanUndo));
        RedoCommand = ReactiveCommand.Create(_history.Redo, this.WhenAnyValue(x => x.CanRedo));
        ZoomInCommand = ReactiveCommand.Create(() => Zoom *= ZoomStep);
        ZoomOutCommand = ReactiveCommand.Create(() => Zoom /= ZoomStep);
        ResetZoomCommand = ReactiveCommand.Create(() => Zoom = 1.0);

        // コマンド内で投げられた例外はダイアログで見せる（未処理のままだとアプリが落ちる）。
        Observable
            .Merge(
                SaveCommand.ThrownExceptions,
                SaveAsCommand.ThrownExceptions,
                AddChildCommand.ThrownExceptions,
                AddSiblingCommand.ThrownExceptions,
                DeleteNodeCommand.ThrownExceptions,
                BeginEditCommand.ThrownExceptions,
                UndoCommand.ThrownExceptions,
                RedoCommand.ThrownExceptions)
            .SelectMany(ex => _showError.Handle(ex.Message))
            .Subscribe();

        _history.Changed += () =>
        {
            CanUndo = _history.CanUndo;
            CanRedo = _history.CanRedo;
        };

        _displayName = this
            .WhenAnyValue(x => x.CurrentFilePath, x => x.IsDirty)
            .Select(t =>
            {
                var name = t.Item1 is null ? _untitledName : Path.GetFileNameWithoutExtension(t.Item1);
                return $"{name}{(t.Item2 ? " *" : string.Empty)}";
            })
            .ToProperty(this, x => x.DisplayName);

        var root = new NodeViewModel(Guid.NewGuid(), "中心テーマ", string.Empty, 480, 300);
        AddNode(root);
        SelectedNode = root;
        IsDirty = false;
    }

    public ObservableCollection<NodeViewModel> Nodes { get; } = new();

    public ObservableCollection<ConnectionViewModel> Connections { get; } = new();

    /// <summary>タブに出す名前。未保存なら末尾に * が付く。</summary>
    public string DisplayName => _displayName.Value;

    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (ReferenceEquals(_selectedNode, value))
            {
                return;
            }

            if (_selectedNode is not null)
            {
                _selectedNode.IsSelected = false;
                _selectedNode.IsEditing = false;
            }

            this.RaiseAndSetIfChanged(ref _selectedNode, value);

            if (_selectedNode is not null)
            {
                _selectedNode.IsSelected = true;
            }
        }
    }

    public string? CurrentFilePath
    {
        get => _currentFilePath;
        private set => this.RaiseAndSetIfChanged(ref _currentFilePath, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => this.RaiseAndSetIfChanged(ref _isDirty, value);
    }

    public bool CanUndo
    {
        get => _canUndo;
        private set => this.RaiseAndSetIfChanged(ref _canUndo, value);
    }

    public bool CanRedo
    {
        get => _canRedo;
        private set => this.RaiseAndSetIfChanged(ref _canRedo, value);
    }

    /// <summary>キャンバスの拡大率。</summary>
    public double Zoom
    {
        get => _zoom;
        set => this.RaiseAndSetIfChanged(ref _zoom, Math.Clamp(value, MinZoom, MaxZoom));
    }

    /// <summary>
    /// スクロール位置。タブは 1 つの表示領域を使い回すので、覚えておかないと
    /// タブを切り替えたときに前のタブの位置がそのまま残ってしまう。
    /// 表示のためだけの値なので通知も保存もしない。
    /// </summary>
    public double ScrollOffsetX { get; set; }

    public double ScrollOffsetY { get; set; }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public ReactiveCommand<Unit, Unit> SaveAsCommand { get; }

    public ReactiveCommand<Unit, Unit> AddChildCommand { get; }

    public ReactiveCommand<Unit, Unit> AddSiblingCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteNodeCommand { get; }

    public ReactiveCommand<Unit, Unit> BeginEditCommand { get; }

    public ReactiveCommand<Unit, Unit> UndoCommand { get; }

    public ReactiveCommand<Unit, Unit> RedoCommand { get; }

    public ReactiveCommand<Unit, double> ZoomInCommand { get; }

    public ReactiveCommand<Unit, double> ZoomOutCommand { get; }

    public ReactiveCommand<Unit, double> ResetZoomCommand { get; }

    /// <summary>指定したファイルの内容で中身を差し替える。</summary>
    public void Load(string path)
    {
        LoadDocument(MindMapFileService.Load(path));
        CurrentFilePath = path;
        IsDirty = false;
        _history.Clear();
    }

    /// <summary>このタブを閉じてよいか。未保存なら保存するかどうかを尋ねる。</summary>
    public async Task<bool> CanCloseAsync()
    {
        if (!IsDirty)
        {
            return true;
        }

        // DisplayName は未保存を示す * が付くので、確認の文面には素のファイル名を渡す。
        var label = CurrentFilePath is null ? _untitledName : Path.GetFileName(CurrentFilePath);

        switch (await _confirmSaveChanges.Handle(label))
        {
            case SaveChangesResult.Save:
                await SaveAsync();

                // 保存先を選ぶダイアログを取り消した場合はまだ未保存なので、閉じずに留まる。
                return !IsDirty;

            case SaveChangesResult.Discard:
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// ドラッグ移動を 1 操作として履歴に積む。移動中の 1 ピクセルごとに積むと
    /// Undo が使い物にならないので、View がドラッグ終了時にだけ呼ぶ。
    /// </summary>
    public void CompleteNodeDrag(NodeViewModel node, double originalX, double originalY)
    {
        if (node.X == originalX && node.Y == originalY)
        {
            return;
        }

        var newX = node.X;
        var newY = node.Y;

        IsDirty = true;
        _history.Push(new DelegateUndoableAction(
            undo: () =>
            {
                node.X = originalX;
                node.Y = originalY;
                SelectedNode = node;
                IsDirty = true;
            },
            redo: () =>
            {
                node.X = newX;
                node.Y = newY;
                SelectedNode = node;
                IsDirty = true;
            }));
    }

    /// <summary>編集中の内容を編集開始時点に戻して確定する（Escape 用）。</summary>
    public void CancelTextEdit()
    {
        if (_activeEdit is not { } edit)
        {
            return;
        }

        edit.Node.Title = edit.Title;
        edit.Node.Body = edit.Body;
        edit.Node.Link = edit.Link;
        edit.Node.IsEditing = false;
    }

    /// <summary>ファイル選択ダイアログを開き、選ばれたファイルをノードのリンクにする。</summary>
    public async Task SetFileLinkAsync(NodeViewModel node)
    {
        var path = await _showLinkFileDialog.Handle(Unit.Default);
        if (!string.IsNullOrEmpty(path))
        {
            SetLink(node, path);
        }
    }

    /// <summary>ノードのリンクを差し替える。空文字を渡すとリンクを外す。1 操作として元に戻せる。</summary>
    public void SetLink(NodeViewModel node, string newLink)
    {
        newLink = newLink?.Trim() ?? string.Empty;
        var oldLink = node.Link;
        if (oldLink == newLink)
        {
            return;
        }

        node.Link = newLink;
        SelectedNode = node;
        IsDirty = true;

        _history.Push(new DelegateUndoableAction(
            undo: () =>
            {
                node.Link = oldLink;
                SelectedNode = node;
                IsDirty = true;
            },
            redo: () =>
            {
                node.Link = newLink;
                SelectedNode = node;
                IsDirty = true;
            }));
    }

    private async Task SaveAsync()
    {
        if (CurrentFilePath is null)
        {
            await SaveAsAsync();
            return;
        }

        MindMapFileService.Save(CurrentFilePath, BuildDocument());
        IsDirty = false;
    }

    private async Task SaveAsAsync()
    {
        var suggested = CurrentFilePath is not null
            ? Path.GetFileName(CurrentFilePath)
            : SuggestFileName();

        var path = await _showSaveFileDialog.Handle(suggested);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        MindMapFileService.Save(path, BuildDocument());
        CurrentFilePath = path;
        IsDirty = false;
    }

    /// <summary>ルートノードのタイトルをファイル名の初期値にする。使えない文字は除去する。</summary>
    private string SuggestFileName()
    {
        var root = Nodes.FirstOrDefault(n => n.Parent is null);
        var baseName = root?.Title.Trim();
        if (string.IsNullOrEmpty(baseName))
        {
            baseName = _untitledName;
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            baseName = baseName.Replace(invalid, '_');
        }

        return baseName + MindMapFileService.FileExtension;
    }

    private void AddChild()
    {
        if (SelectedNode is not { } parent)
        {
            return;
        }

        // 既存の子の下に積む。まだ子がいなければ親の真横に置く。
        var siblings = ChildrenOf(parent);
        var y = siblings.Count == 0
            ? parent.Y
            : siblings.Max(n => n.Y + n.Height) + VerticalGap;

        InsertNode(new NodeViewModel(
            Guid.NewGuid(),
            "新しいノード",
            string.Empty,
            parent.X + parent.Width + HorizontalGap,
            y)
        {
            Parent = parent,
        });
    }

    private void AddSibling()
    {
        if (SelectedNode is not { } node)
        {
            return;
        }

        // ルートには兄弟を作れないので、代わりに子を足す。
        if (node.Parent is not { } parent)
        {
            AddChild();
            return;
        }

        var siblings = ChildrenOf(parent);
        var y = siblings.Max(n => n.Y + n.Height) + VerticalGap;

        InsertNode(new NodeViewModel(Guid.NewGuid(), "新しいノード", string.Empty, node.X, y)
        {
            Parent = parent,
        });
    }

    private List<NodeViewModel> ChildrenOf(NodeViewModel parent) =>
        Nodes.Where(n => ReferenceEquals(n.Parent, parent)).ToList();

    private void InsertNode(NodeViewModel node)
    {
        AddNode(node);
        SelectedNode = node;
        node.IsEditing = true;
        IsDirty = true;

        _history.Push(new DelegateUndoableAction(
            undo: () =>
            {
                SelectedNode = node.Parent;
                RemoveNode(node);
                IsDirty = true;
            },
            redo: () =>
            {
                AddNode(node);
                SelectedNode = node;
                IsDirty = true;
            }));
    }

    private void DeleteSelectedNode()
    {
        // ルートは canDelete で弾いているのでここには来ない。
        if (SelectedNode is not { Parent: { } parent } target)
        {
            return;
        }

        var subtree = CollectSubtree(target);

        SelectedNode = parent;
        foreach (var node in subtree)
        {
            RemoveNode(node);
        }

        IsDirty = true;

        _history.Push(new DelegateUndoableAction(
            undo: () =>
            {
                // 親を先に戻さないと接続線が張れないので、CollectSubtree の順序（親→子）を保つ。
                foreach (var node in subtree)
                {
                    AddNode(node);
                }

                SelectedNode = target;
                IsDirty = true;
            },
            redo: () =>
            {
                SelectedNode = parent;
                foreach (var node in subtree)
                {
                    RemoveNode(node);
                }

                IsDirty = true;
            }));
    }

    private void BeginEditSelectedNode()
    {
        if (SelectedNode is { } node)
        {
            node.IsEditing = true;
        }
    }

    /// <summary>自分自身を含む部分木を親から順に返す。削除時に子孫を取り残さないため。</summary>
    private List<NodeViewModel> CollectSubtree(NodeViewModel root)
    {
        var result = new List<NodeViewModel>();
        var queue = new Queue<NodeViewModel>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var child in Nodes.Where(n => ReferenceEquals(n.Parent, current)))
            {
                queue.Enqueue(child);
            }
        }

        return result;
    }

    private void AddNode(NodeViewModel node)
    {
        Nodes.Add(node);

        if (node.Parent is not null)
        {
            Connections.Add(new ConnectionViewModel(node.Parent, node));
        }

        _nodeSubscriptions[node.Id] = new CompositeDisposable(
            node.Changed
                .Where(e => e.PropertyName is nameof(NodeViewModel.Title)
                    or nameof(NodeViewModel.Body)
                    or nameof(NodeViewModel.Link)
                    or nameof(NodeViewModel.X)
                    or nameof(NodeViewModel.Y))
                .Subscribe(_ => IsDirty = true),
            node.WhenAnyValue(n => n.IsEditing)
                .Skip(1)
                .Subscribe(isEditing => OnEditingChanged(node, isEditing)));
    }

    /// <summary>
    /// 編集の開始から終了までを 1 回の Undo 単位にまとめる。1 文字ごとに履歴を積むと
    /// Undo が実用にならないため、確定時に差分があったときだけ積む。
    /// </summary>
    private void OnEditingChanged(NodeViewModel node, bool isEditing)
    {
        if (isEditing)
        {
            _activeEdit = (node, node.Title, node.Body, node.Link);
            return;
        }

        if (_activeEdit is not { } edit || !ReferenceEquals(edit.Node, node))
        {
            return;
        }

        _activeEdit = null;

        if (edit.Title == node.Title && edit.Body == node.Body && edit.Link == node.Link)
        {
            return;
        }

        // タイトル・本文・リンクはひとつの編集操作なので、まとめて 1 回の Undo にする。
        var before = (edit.Title, edit.Body, edit.Link);
        var after = (node.Title, node.Body, node.Link);

        _history.Push(new DelegateUndoableAction(
            undo: () => Restore(node, before),
            redo: () => Restore(node, after)));
    }

    private void Restore(NodeViewModel node, (string Title, string Body, string Link) content)
    {
        node.Title = content.Title;
        node.Body = content.Body;
        node.Link = content.Link;
        SelectedNode = node;
        IsDirty = true;
    }

    private void RemoveNode(NodeViewModel node)
    {
        foreach (var connection in Connections
                     .Where(c => ReferenceEquals(c.Child, node) || ReferenceEquals(c.Parent, node))
                     .ToList())
        {
            Connections.Remove(connection);
            connection.Dispose();
        }

        if (_nodeSubscriptions.Remove(node.Id, out var subscription))
        {
            subscription.Dispose();
        }

        Nodes.Remove(node);
    }

    private void Clear()
    {
        SelectedNode = null;
        _activeEdit = null;

        foreach (var connection in Connections)
        {
            connection.Dispose();
        }

        Connections.Clear();

        foreach (var subscription in _nodeSubscriptions.Values)
        {
            subscription.Dispose();
        }

        _nodeSubscriptions.Clear();
        Nodes.Clear();
    }

    private MindMapDocument BuildDocument() => new()
    {
        Version = MindMapDocument.CurrentVersion,
        Nodes = Nodes
            .Select(n => new MindMapNodeDto
            {
                Id = n.Id,
                ParentId = n.Parent?.Id,
                Title = n.Title,
                Body = n.Body,
                Link = n.Link,
                X = n.X,
                Y = n.Y,
            })
            .ToList(),
    };

    private void LoadDocument(MindMapDocument document)
    {
        Clear();

        // 親子を結ぶ前に全ノードを実体化しておく（ファイル内の並び順に依存しないため）。
        var byId = new Dictionary<Guid, NodeViewModel>();
        foreach (var dto in document.Nodes)
        {
            // 旧形式（Version 1）のファイルはタイトルが Text 欄に入っている。
            byId[dto.Id] = new NodeViewModel(dto.Id, dto.ResolveTitle(), dto.Body, dto.X, dto.Y, dto.Link);
        }

        foreach (var dto in document.Nodes)
        {
            if (dto.ParentId is { } parentId
                && byId.TryGetValue(parentId, out var parent)
                && !CreatesCycle(byId[dto.Id], parent))
            {
                byId[dto.Id].Parent = parent;
            }
        }

        foreach (var node in byId.Values)
        {
            AddNode(node);
        }

        SelectedNode = Nodes.FirstOrDefault(n => n.Parent is null) ?? Nodes.FirstOrDefault();
    }

    /// <summary>
    /// 壊れたファイルで親子関係が輪になっていると、部分木の走査が終わらなくなる。
    /// 輪を作る親子付けは読み込み時点で捨てる。
    /// </summary>
    private static bool CreatesCycle(NodeViewModel node, NodeViewModel candidateParent)
    {
        for (var current = candidateParent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, node))
            {
                return true;
            }
        }

        return false;
    }
}
