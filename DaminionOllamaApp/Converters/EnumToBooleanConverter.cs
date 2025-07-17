// DaminionOllamaApp/Converters/EnumToBooleanConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace DaminionOllamaApp.Converters
{
    /// <summary>
    /// Converts between an enum value and a boolean for use with RadioButton binding in WPF.
    /// </summary>
    public class EnumToBooleanConverter : IValueConverter
    {
        /// <summary>
        /// Converts an enum value to a boolean (true if the value matches the parameter).
        /// </summary>
        /// <param name="value">The enum value from the binding source.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The specific enum value to check for.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>True if the value matches the parameter; otherwise, false.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null || parameter is null)
                return false;

            string? enumValue = value?.ToString();
            string? targetValue = parameter?.ToString();
            if (enumValue is null || targetValue is null)
                return false;

            return enumValue.Equals(targetValue, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Converts a boolean (from a checked RadioButton) back to the corresponding enum value.
        /// </summary>
        /// <param name="value">The boolean value from the binding target.</param>
        /// <param name="targetType">The type to convert to (the enum type).</param>
        /// <param name="parameter">The specific enum value this RadioButton represents.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>The enum value corresponding to the parameter if the RadioButton is checked.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If the RadioButton is checked (value is true), return the enum value it represents.
            if (value is bool isChecked && isChecked && parameter is not null)
            {
                return Enum.Parse(targetType, parameter.ToString()!);
            }
            // Otherwise, do nothing.
            return Binding.DoNothing;
        }
    }
}