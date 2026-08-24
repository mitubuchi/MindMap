using System.Text;
using System.Windows;
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

        var window = new MainWindow();
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
