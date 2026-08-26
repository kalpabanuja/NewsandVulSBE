using System;
using System.Globalization;
using Microsoft.Maui.Graphics;

namespace ThreatIntelClient.Converters;

public class CvssToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double cvss)
        {
            if (cvss >= 9.0) return Colors.Crimson; // CRITICAL
            if (cvss >= 7.0) return Colors.Orange; // HIGH
            if (cvss >= 4.0) return Color.FromArgb("#FFC107"); // Amber / MEDIUM
            return Colors.Green; // LOW
        }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
