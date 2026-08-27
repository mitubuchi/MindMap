using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MindMap.Abstractions.Thumbnails;

namespace MindMap.Services.Thumbnails;

/// <summary>
/// ノードのリンク先から小さな絵を作って配る。作り方はパッケージ（<see cref="ThumbnailRegistry"/>）が持ち、
/// ここは「いつ作るか・どこで作るか・作ったものをどう使い回すか」だけを見る。
///
/// 作る場所を UI スレッドから外してあるのが要点。画像のデコードも、動画のポスターフレームの
/// 取り出しも 1 枚ずつは短いが、マップを開いた瞬間に何十枚も作るので、UI スレッドで回すと
/// そのぶん画面が止まる。かといって普通のスレッドプールにも投げられない
/// （OS のサムネイルは COM を使うので STA から呼ぶ必要がある）。
/// そこで STA の作業スレッドを 1 本だけ立てて、そこに順番に流す。
/// </summary>
public sealed class NodeThumbnailService : IDisposable
{
    /// <summary>一辺の目安。ノード側の枠もこの大きさで用意する。</summary>
    public const int Size = 256;

    private readonly ThumbnailRegistry _registry;

    /// <summary>
    /// 同じファイルを何度も作り直さないための控え。
    /// 鍵にファイルの更新日時と大きさを混ぜてあるので、差し替えられた画像は作り直される。
    /// </summary>
    private readonly Dictionary<string, Task<BitmapSource?>> _cache = new();

    private readonly Lazy<Dispatcher> _worker;

    private bool _disposed;

    public NodeThumbnailService(ThumbnailRegistry registry)
    {
        _registry = registry;

        // 1 枚も要らないマップでは作業スレッドも立てない。
        _worker = new Lazy<Dispatcher>(StartWorker);
    }

    /// <summary>
    /// 絵を作る（すでに作ってあれば、そのまま返す）。
    /// 作れないとき（パッケージが無い・扱えない種類・ファイルが無い）は null。
    ///
    /// UI スレッドから呼ぶこと。実際の生成だけが作業スレッドへ回る。
    /// </summary>
    public Task<BitmapSource?> GetAsync(string? absolutePath)
    {
        if (_disposed || _registry.IsEmpty || string.IsNullOrEmpty(absolutePath))
        {
            return Task.FromResult<BitmapSource?>(null);
        }

        FileInfo info;
        try
        {
            info = new FileInfo(absolutePath);
            if (!info.Exists)
            {
                return Task.FromResult<BitmapSource?>(null);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException
                                       or IOException or UnauthorizedAccessException)
        {
            // 読めない場所を指していた、というだけ。絵が付かないノードとして扱う。
            return Task.FromResult<BitmapSource?>(null);
        }

        var request = new ThumbnailRequest(info.FullName, Size);
        if (_registry.Resolve(request) is not { } provider)
        {
            return Task.FromResult<BitmapSource?>(null);
        }

        // 同じ中身なら作り直さない。差し替えられたら（更新日時か大きさが変われば）別の鍵になる。
        var key = $"{info.FullName}|{info.LastWriteTimeUtc.Ticks}|{info.Length}";

        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var task = RunAsync(provider, request);
        _cache[key] = task;
        return task;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_worker.IsValueCreated)
        {
            _worker.Value.InvokeShutdown();
        }
    }

    private async Task<BitmapSource?> RunAsync(INodeThumbnailProvider provider, ThumbnailRequest request)
    {
        try
        {
            var image = await _worker.Value
                .InvokeAsync(() => provider.GetAsync(request, CancellationToken.None))
                .Task
                .Unwrap();

            // 凍っていない絵は UI スレッドで描けない。約束を守らないパッケージがあっても
            // 落とさず、絵の無いノードとして扱う（片方のスレッドで作ったものを
            // もう片方で触ると、原因の分かりにくい例外になるため）。
            return image is { IsFrozen: true } ? AsPixelSized(image) : null;
        }
        catch (Exception)
        {
            // 壊れたファイル、対応していない形式、パッケージ側の不具合。
            // どれも「絵が付かない」で済ませる。ノードは本文つきの見た目のまま出る。
            return null;
        }
    }

    /// <summary>
    /// 絵の解像度（dpi）を 96 に直す。中身のピクセルはそのままで、大きさの言い方だけを変える。
    ///
    /// 頼むときの一辺（<see cref="Size"/>）はピクセルで数えているのに、WPF は絵の実寸を
    /// 「ピクセル数 × 96 ÷ dpi」で測る。元の写真の dpi を引き継いだ絵をそのまま渡すと、
    /// 数字が食い違う。350 dpi で撮る機種（Sony の α など）では 256px の絵が 70 相当と見なされ、
    /// 引き伸ばさない指定（StretchDirection="DownOnly"）と噛み合って、枠の中で小さく描かれていた。
    ///
    /// 直すのはホスト側の仕事。置き方（枠の大きさ・中央寄せ・引き伸ばさないこと）を決めているのが
    /// ホストなので、渡された絵をその物差しに合わせるところまで持つ。
    /// </summary>
    private static BitmapSource AsPixelSized(BitmapSource image)
    {
        if (Math.Abs(image.DpiX - 96) < 0.5 && Math.Abs(image.DpiY - 96) < 0.5)
        {
            return image;
        }

        try
        {
            var stride = (image.PixelWidth * image.Format.BitsPerPixel + 7) / 8;
            var pixels = new byte[stride * image.PixelHeight];
            image.CopyPixels(pixels, stride, 0);

            var copy = BitmapSource.Create(
                image.PixelWidth,
                image.PixelHeight,
                96,
                96,
                image.Format,
                image.Palette,
                pixels,
                stride);

            copy.Freeze();
            return copy;
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or OverflowException)
        {
            // 写し取れない形式だった。小さく出るとしても、絵が無いよりはましなので元のまま返す。
            return image;
        }
    }

    /// <summary>
    /// 絵を作るための STA スレッドを 1 本立てる。
    ///
    /// スレッドに <see cref="Dispatcher"/> を載せているのは、順番待ちの列と
    /// await できる呼び出し口をここで作らずに済ませるため（WPF の Dispatcher は
    /// UI スレッド専用ではなく、どのスレッドでも動く）。
    /// </summary>
    private static Dispatcher StartWorker()
    {
        var ready = new TaskCompletionSource<Dispatcher>();

        var thread = new Thread(() =>
        {
            ready.SetResult(Dispatcher.CurrentDispatcher);
            Dispatcher.Run();
        })
        {
            // 画面を閉じるときに、作りかけの絵を待たない。
            IsBackground = true,
            Name = "MindMap thumbnails",
        };

        // OS のサムネイル（動画のポスターフレーム）が COM を使うので STA にする。
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return ready.Task.GetAwaiter().GetResult();
    }
}
