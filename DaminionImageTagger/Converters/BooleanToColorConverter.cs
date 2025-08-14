using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DaminionImageTagger.Converters
{
    /// <summary>
    /// Converts boolean values to colors based on a parameter
    /// </summary>
    public class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string param)
            {
                var parts = param.Split('|');
                if (parts.Length == 2)
                {
                    var colorString = boolValue ? parts[0] : parts[1];
                    return (SolidColorBrush)new BrushConverter().ConvertFromString(colorString);
                }
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
