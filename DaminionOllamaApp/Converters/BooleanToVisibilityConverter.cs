using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DaminionOllamaApp.Converters
{
    /// <summary>
    /// Converts a boolean value to a Visibility value for WPF data binding.
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean to Visibility.Visible or Visibility.Collapsed.
        /// </summary>
        /// <param name="value">The boolean value from the binding source.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>Visibility.Visible if true; otherwise, Visibility.Collapsed.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        /// <summary>
        /// Converts a Visibility value back to a boolean.
        /// </summary>
        /// <param name="value">The Visibility value from the binding target.</param>
        /// <param name="targetType">The type to convert to (bool).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>True if Visibility.Visible; otherwise, false.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
                return v == Visibility.Visible;
            return false;
        }
    }
} 