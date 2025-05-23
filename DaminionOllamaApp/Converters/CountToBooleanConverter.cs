﻿// DaminionOllamaApp/Converters/CountToBooleanConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace DaminionOllamaApp.Converters // Or DaminionOllamaApp.Views if you prefer to keep converters with views
{
    public class CountToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}