using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DaminionTorchTrainer.Converters
{
    /// <summary>
    /// Converts boolean values to Visibility values
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a Visibility value
        /// </summary>
        /// <param name="value">The boolean value to convert</param>
        /// <param name="targetType">The target type (Visibility)</param>
        /// <param name="parameter">Optional parameter (can be "Inverted" to invert the logic)</param>
        /// <param name="culture">The culture information</param>
        /// <returns>Visibility.Visible if true, Visibility.Collapsed if false</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Check if we should invert the logic
                bool invert = parameter?.ToString()?.ToLower() == "inverted";
                bool result = invert ? !boolValue : boolValue;
                
                return result ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }

        /// <summary>
        /// Converts a Visibility value back to a boolean value
        /// </summary>
        /// <param name="value">The Visibility value to convert</param>
        /// <param name="targetType">The target type (bool)</param>
        /// <param name="parameter">Optional parameter (can be "Inverted" to invert the logic)</param>
        /// <param name="culture">The culture information</param>
        /// <returns>True if Visible, false otherwise</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                // Check if we should invert the logic
                bool invert = parameter?.ToString()?.ToLower() == "inverted";
                bool result = visibility == Visibility.Visible;
                
                return invert ? !result : result;
            }
            
            return false;
        }
    }
}
