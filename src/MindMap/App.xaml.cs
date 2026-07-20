using System.Windows;
using ReactiveUI.Builder;

namespace MindMap;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ReactiveUI 20 以降は明示的な初期化が必須。WhenAnyValue などを最初に触る前に済ませる必要が
        // あるため、StartupUri は使わず MainWindow の生成もここで行う。
        RxAppBuilder
            .CreateReactiveUIBuilder()
            .WithCoreServices()
            .WithWpf()
            .Build();

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
