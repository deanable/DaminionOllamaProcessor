// DaminionOllamaApp/Converters/CountToBooleanConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace DaminionOllamaApp.Converters // Or DaminionOllamaApp.Views if you prefer to keep converters with views
{
    /// <summary>
    /// Converts an integer count to a boolean (true if count > 0).
    /// Useful for enabling/disabling controls based on collection size.
    /// </summary>
    public class CountToBooleanConverter : IValueConverter
    {
        /// <summary>
        /// Converts an integer count to a boolean.
        /// </summary>
        /// <param name="value">The integer count from the binding source.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>True if count > 0; otherwise, false.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 0;
            }
            return false;
        }

        /// <summary>
        /// Not implemented. Throws NotImplementedException.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}