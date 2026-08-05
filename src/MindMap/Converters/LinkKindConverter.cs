using System.Globalization;
using System.Windows.Data;
using MindMap.Services;

namespace MindMap.Converters;

/// <summary>
/// ノードのリンクを <see cref="LinkKind"/> に変換する。
/// View 側はこれを見て、種類ごとの線画アイコンに切り替える。
/// </summary>
public sealed class LinkKindConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ShellIconService.Classify(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
