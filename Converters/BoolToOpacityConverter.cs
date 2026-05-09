// converters/booltoopacityconverter.cs конвертер isenabled → opacity для кнопок пагинации
using System.Globalization;
using System.Windows.Data;

namespace MovieApp
{
    /// <summary> конвертирует булевое значение в уровень прозрачности: true → 1.0 (кнопка активна, полностью видима) false → 0.4 (кнопка отключена, визуально приглушена) используется для кнопок «назад» / «вперёд» в пагинации каталога. </summary>
    public sealed class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? 1.0 : 0.4;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}