using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MindMap.Converters;

/// <summary>
/// 空かどうかで見せ方を決める。空文字・0・空の一覧をまとめて「空」と見なす。
/// <see cref="Invert"/> を立てると「中身があるときだけ見せる」になる。
///
/// ステータスバー（ツールの結果が出ていない間だけ案内を出す）と、
/// ツールバー（パッケージのツールが 1 つも無ければ区切り線も出さない）で使う。
/// </summary>
public sealed class EmptyToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var empty = value switch
        {
            null => true,
            string text => string.IsNullOrWhiteSpace(text),
            int count => count == 0,
            ICollection collection => collection.Count == 0,
            _ => false,
        };

        return empty != Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
