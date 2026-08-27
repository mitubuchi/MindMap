using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace MindMap.Services;

/// <summary>
/// URL やファイル・フォルダーを OS に渡して開く。
///
/// ノードのリンクからも、フォルダーの一覧の行からも同じ経路で開くようにまとめてある
/// （関連付けが無いときの振る舞いが 2 か所でずれないように）。
/// </summary>
public static class ShellOpenService
{
    /// <summary>関連付けが無いファイルを開こうとしたときに Windows が返すコード。</summary>
    private const int NoAssociationErrorCode = 1155;

    /// <summary>
    /// URL は既定のブラウザーで、ファイルは関連付けられたアプリで、
    /// フォルダーはエクスプローラーで開く。開けなければ理由を出す。
    /// </summary>
    /// <param name="owner">エラーを出すときの親ウィンドウ。無ければ画面中央に出る。</param>
    public static void Open(string target, Window? owner = null)
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
            var message = $"開けませんでした。\n\n{target}\n\n{ex.Message}";

            if (owner is not null)
            {
                MessageBox.Show(owner, message, "MindMap", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show(message, "MindMap", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
