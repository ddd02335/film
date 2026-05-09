// converters/booltopagebuttonstyleconverter.cs
// конвертер: isactive=true → стиль активной страницы (красный фон),
// isactive=false → стиль обычной страницы (прозрачный фон)
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MovieApp
{
    public sealed class BoolToPageButtonStyleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isActive = value is true;
            string key    = isActive ? "ActivePageNumberButton" : "PageNumberButton";

            // ищем стиль в ресурсах приложения
            return Application.Current.FindResource(key) as Style
                   ?? DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}