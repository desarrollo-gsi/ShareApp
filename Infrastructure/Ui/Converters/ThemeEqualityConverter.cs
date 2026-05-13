using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AvaloniaShareApp.Infrastructure.Ui.Converters
{
    public class ThemeEqualityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string currentTheme = value as string ?? "";
            string targetTheme = parameter as string ?? "";
            
            if (currentTheme == targetTheme)
            {
                return Brush.Parse("#3B82F6"); // Active Blue color
            }
            
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
