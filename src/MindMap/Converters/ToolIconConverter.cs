using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MindMap.Converters;

/// <summary>
/// マニフェストに書かれたアイコンの図形（Path のミニ言語）を <see cref="Geometry"/> にする。
///
/// 書かれていない場合と、読めない文字列だった場合は、どちらも既定の印に戻す。
/// パッケージの書き間違いでツールバーのボタンが消えるより、
/// 見た目が揃わないほうがましなため。
/// </summary>
public sealed class ToolIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string figures && figures.Trim().Length > 0)
        {
            try
            {
                return Geometry.Parse(figures);
            }
            catch (FormatException)
            {
                // 既定の印に落とす。
            }
        }

        return Application.Current?.TryFindResource("Icon.Tool");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
