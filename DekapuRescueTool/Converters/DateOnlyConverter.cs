using System.Globalization;
using System.Windows.Data;

namespace DekapuRescueTool.Converters;

[ValueConversion(typeof(DateOnly), typeof(string))]
public class DateOnlyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is DateOnly d ? d.ToString("yyyy-MM-dd") : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
