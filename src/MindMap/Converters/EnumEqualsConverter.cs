using System.Globalization;
using System.Windows.Data;

namespace MindMap.Converters;

/// <summary>
/// 値が ConverterParameter と同じかどうか。列挙型の選択肢をラジオボタンに割り当てるために使う。
/// 書き戻すのは選ばれた側だけで、外れた側は何もしない
/// （2 つのボタンが同時に書き戻して、片方の値で上書きされるのを避けるため）。
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null &&
        parameter is not null &&
        string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null)
        {
            return Binding.DoNothing;
        }

        // 対になる列挙型は束縛先の型から分かるので、変換器側では型を知らずに済む。
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;

        return Enum.TryParse(type, parameter.ToString(), out var parsed) ? parsed : Binding.DoNothing;
    }
}
