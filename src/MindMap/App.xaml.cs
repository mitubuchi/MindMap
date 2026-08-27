using System.Text;
using System.Windows;
using MindMap.Services.Packages;
using MindMap.Services.Thumbnails;
using MindMap.Services.Tools;
using MindMap.Services.Viewers;
using ReactiveUI.Builder;

namespace MindMap;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // BOM の無いテキストを Shift_JIS などで読み直せるようにする。
        // .NET では既定で UTF-8 系しか引けないので、ビューアが使う前にここで足しておく。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // ReactiveUI 20 以降は明示的な初期化が必須。WhenAnyValue などを最初に触る前に済ませる必要が
        // あるため、StartupUri は使わず MainWindow の生成もここで行う。
        RxAppBuilder
            .CreateReactiveUIBuilder()
            .WithCoreServices()
            .WithWpf()
            .Build();

        // 提供物の置き場。種類ごとに 1 つずつ用意して、plugins のパッケージから配ってもらう。
        // 表示（ビューア）は組み込みぶんを持って始まり、ツールとサムネイルは空から始まる。
        var viewers = new ViewerRegistry();
        var tools = new MapToolRegistry();
        var thumbnails = new ThumbnailRegistry();
        var packages = PackageLoader.LoadAll(viewers, tools, thumbnails);

        // 壊れたパッケージは黙って無視せず 1 度だけ知らせる（入れたのに効かない状態が
        // いちばん分かりにくいため）。読めたぶんはそのまま使える。
        if (packages.Errors.Count > 0)
        {
            MessageBox.Show(
                string.Join(Environment.NewLine, packages.Errors),
                "パッケージを読み込めませんでした",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        var window = new MainWindow(viewers, tools, thumbnails);
        MainWindow = window;
        window.Show();

        // 拡張子の関連付けから起動されると、開くファイルが引数で渡ってくる。
        // ダイアログの表示先が要るので、ウィンドウを出してから開く。
        if (e.Args.Length > 0 && window.ViewModel is { } viewModel)
        {
            viewModel.OpenFiles(e.Args);
        }
    }
}
