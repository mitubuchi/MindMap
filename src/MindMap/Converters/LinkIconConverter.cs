using System.Globalization;
using System.Windows.Data;
using MindMap.Services;

namespace MindMap.Converters;

/// <summary>
/// ノードのリンク（URL やファイルのパス）を、その種類を表すアイコンに変換する。
/// 種類が分からないときは null を返し、View 側が既定のリンク記号に戻す。
/// </summary>
public sealed class LinkIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ShellIconService.ForLink(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
