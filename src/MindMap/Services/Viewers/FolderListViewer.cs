using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using MindMap.Abstractions.Viewers;
using MindMap.Services.Printing;

namespace MindMap.Services.Viewers;

/// <summary>
/// リンク先がフォルダーのとき、中身を一覧で出す。名前・サイズ・更新日時。
///
/// 組み込みにしているのは、外部のものを何も使わずに書けて、
/// エクスプローラーからノードにドラッグするとフォルダーへのリンクが日常的にできるため。
/// </summary>
public sealed class FolderListViewerFactory : IContentViewerFactory
{
    public string Id => "builtin.folder";

    public int Priority => 100;

    public bool CanView(ViewerContent content) => content.IsDirectory;

    public IContentViewer Create() => new FolderListViewer();
}

/// <summary>一覧に出す中身。<see cref="FolderListView"/> の DataContext になる。</summary>
public sealed class FolderListModel
{
    public IReadOnlyList<FolderEntry> Entries { get; init; } = [];

    /// <summary>件数と合計サイズ。一覧の上に出す。</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>一覧を出せないときの理由。</summary>
    public string? Message { get; init; }

    public bool HasEntries => Entries.Count > 0;
}

internal sealed class FolderListViewer : IContentViewer, IPrintableViewer
{
    /// <summary>
    /// これより多い項目は出さない。数万件のフォルダーを選んだだけで
    /// 列挙と組み立てに時間がかかってしまうため。
    /// </summary>
    private const int MaxEntries = 500;

    private const string DateFormat = "yyyy/MM/dd HH:mm";

    private readonly FolderListView _view = new();

    /// <summary>印刷の見出しにする、フォルダーの場所。</summary>
    private string _path = string.Empty;

    public FrameworkElement View => _view;

    public async Task LoadAsync(ViewerContent content, CancellationToken cancellationToken)
    {
        // 数が多いと列挙だけで時間がかかるので、別のスレッドに逃がす。
        var model = await Task
            .Run(() => Build(content.FilePath, cancellationToken), cancellationToken)
            .ConfigureAwait(false);

        // 続きが UI スレッドに戻るかは呼ばれ方で変わる。画面を触るところは明示的に渡す。
        if (_view.Dispatcher.CheckAccess())
        {
            Apply();
            return;
        }

        await _view.Dispatcher.InvokeAsync(Apply);

        void Apply()
        {
            _path = content.FilePath;
            _view.DataContext = model;
        }
    }

    /// <summary>画面と同じ一覧を表にして印刷する。アイコンは出さない。</summary>
    public DocumentPaginator? CreatePaginator(PrintPage page)
    {
        if (_view.DataContext is not FolderListModel { HasEntries: true } model)
        {
            return null;
        }

        var document = TextPrintDocument.Begin(_path);

        document.Blocks.Add(new Paragraph(new Run(model.Summary))
        {
            Foreground = new SolidColorBrush(Color.FromRgb(0x5B, 0x66, 0x73)),
            Margin = new Thickness(0, 0, 0, 8),
        });

        // 列幅はすべて比率で決める。固定幅と * を混ぜると、FlowDocument の表では
        // * の列が 1 文字ぶんまで潰れて名前が縦に並んでしまう。
        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(6, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1.3, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });

        var rows = new TableRowGroup();
        rows.Rows.Add(Row(FontWeights.SemiBold, "名前", "サイズ", "更新日時"));

        foreach (var entry in model.Entries)
        {
            rows.Rows.Add(Row(FontWeights.Normal, entry.Name, entry.Size, entry.Modified));
        }

        table.RowGroups.Add(rows);
        document.Blocks.Add(table);

        return PrintLayout.Paginate(document, page);

        static TableRow Row(FontWeight weight, string name, string size, string modified)
        {
            var row = new TableRow { FontWeight = weight };
            row.Cells.Add(Cell(name, TextAlignment.Left));
            row.Cells.Add(Cell(size, TextAlignment.Right));
            row.Cells.Add(Cell(modified, TextAlignment.Right));
            return row;
        }

        static TableCell Cell(string text, TextAlignment alignment) =>
            new(new Paragraph(new Run(text)) { Margin = new Thickness(0), TextAlignment = alignment })
            {
                Padding = new Thickness(0, 2, 6, 2),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE4, 0xE8, 0xED)),
                BorderThickness = new Thickness(0, 0, 0, 0.5),
            };
    }

    public void Dispose()
    {
        // 画面を抱えているだけなので、手放すものはない。
    }

    private static FolderListModel Build(string path, CancellationToken cancellationToken)
    {
        List<FileSystemInfo> found;

        try
        {
            // 1 回で読み切る。列挙の途中で権限に当たると、そこまでの分も失うので
            // ここでまとめて受け止める。
            found = new DirectoryInfo(path).EnumerateFileSystemInfos().ToList();
        }
        catch (UnauthorizedAccessException)
        {
            return new FolderListModel { Message = "このフォルダーを読む権限がありません。" };
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            return new FolderListModel { Message = $"フォルダーを読めませんでした。\n\n{ex.Message}" };
        }

        cancellationToken.ThrowIfCancellationRequested();

        // フォルダーを先に、それぞれ名前順。エクスプローラーの既定と同じ並びにする。
        var folders = found.OfType<DirectoryInfo>()
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var files = found.OfType<FileInfo>()
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (folders.Count == 0 && files.Count == 0)
        {
            return new FolderListModel { Message = "このフォルダーは空です。" };
        }

        var entries = new List<FolderEntry>(Math.Min(folders.Count + files.Count, MaxEntries));

        foreach (var folder in folders.Take(MaxEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(Entry(folder, size: null));
        }

        foreach (var file in files.Take(MaxEntries - entries.Count))
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(Entry(file, Length(file)));
        }

        return new FolderListModel
        {
            Entries = entries,
            Summary = Summarize(folders.Count, files.Count, files.Sum(Length), entries.Count),
        };
    }

    private static FolderEntry Entry(FileSystemInfo info, long? size) => new()
    {
        // 絵柄はこのパスから引かれる。フォルダーはフォルダーの線画、
        // ファイルは関連付けられたアプリのアイコンになる（ノードの脇と同じ）。
        Link = info.FullName,
        Name = info.Name,
        Size = size is { } bytes ? ByteSize.Format(bytes) : string.Empty,
        Modified = Modified(info),
    };

    private static string Summarize(int folders, int files, long total, int shown)
    {
        var summary = string.Create(
            CultureInfo.CurrentCulture,
            $"フォルダー {folders:N0}・ファイル {files:N0}（{ByteSize.Format(total)}）");

        var remaining = folders + files - shown;

        return remaining > 0
            ? summary + string.Create(CultureInfo.CurrentCulture, $" — 先頭 {shown:N0} 件のみ表示")
            : summary;
    }

    /// <summary>更新日時。読めない項目でも一覧からは外さず、日時だけ空にする。</summary>
    private static string Modified(FileSystemInfo info)
    {
        try
        {
            var time = info.LastWriteTime;
            return time.Year > 1601 ? time.ToString(DateFormat, CultureInfo.CurrentCulture) : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            return string.Empty;
        }
    }

    private static long Length(FileInfo file)
    {
        try
        {
            return file.Length;
        }
        catch (IOException)
        {
            // 列挙のあとに消えた・切断された。0 として扱い、一覧自体は出す。
            return 0;
        }
    }
}
