using System.Reactive;
using System.Windows;
using Microsoft.Win32;
using MindMap.Services.Settings;
using MindMap.ViewModels;
using ReactiveUI;

namespace MindMap;

/// <summary>
/// 設定ウィンドウ。中身は <see cref="SettingsViewModel"/> が
/// <see cref="AppSettings.Definitions"/> から組み立てるので、
/// 設定項目が増えてもここは変わらない。
/// </summary>
public partial class SettingsWindow : Window, IViewFor<SettingsViewModel>
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(SettingsViewModel),
        typeof(SettingsWindow),
        new PropertyMetadata(null));

    public SettingsWindow()
    {
        InitializeComponent();

        var viewModel = new SettingsViewModel(SettingsService.Current);
        ViewModel = viewModel;
        DataContext = viewModel;

        viewModel.ShowBrowseDialog.RegisterHandler(context =>
            context.SetOutput(Browse(context.Input)));

        viewModel.ShowError.RegisterHandler(context =>
        {
            MessageBox.Show(this, context.Input, "MindMap", MessageBoxButton.OK, MessageBoxImage.Error);
            context.SetOutput(Unit.Default);
        });

        // 保存できたときだけ閉じる。書き込めなかったときは入力を残して開いたままにする。
        viewModel.OkCommand.Subscribe(saved =>
        {
            if (saved)
            {
                DialogResult = true;
            }
        });
    }

    public SettingsViewModel? ViewModel
    {
        get => (SettingsViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = (SettingsViewModel?)value;
    }

    /// <summary>参照ボタン。いま入っている値を、選択ダイアログの開き先にする。</summary>
    private string? Browse(SettingItemViewModel item)
    {
        var current = item.Value.Trim();

        if (item.Kind == SettingKind.File)
        {
            var file = new OpenFileDialog
            {
                Title = item.Label,
                Filter = "すべてのファイル (*.*)|*.*",
                CheckFileExists = true,
                FileName = current,
            };

            return file.ShowDialog(this) == true ? file.FileName : null;
        }

        var folder = new OpenFolderDialog
        {
            Title = item.Label,
            // 消えているフォルダーが書かれていても、ダイアログが出ないと直せない。
            InitialDirectory = System.IO.Directory.Exists(current) ? current : string.Empty,
        };

        return folder.ShowDialog(this) == true ? folder.FolderName : null;
    }
}
