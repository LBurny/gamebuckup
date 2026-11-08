using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using GameBackup.Core.Models;
using GameBackup.Plugin.Adapters;

namespace GameBackup.Plugin.UI.Converters
{
    public sealed class PathToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var width = 96;
            if (parameter != null && int.TryParse(parameter.ToString(), out var parsed))
            {
                width = parsed;
            }
            return ImageLoader.Load(value as string, width);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public sealed class StatusBrushConverter : IValueConverter
    {
        private static readonly Brush UnknownFallback = CreateBrush("#8A8A8A");
        private static readonly Brush SuccessFallback = CreateBrush("#54B054");
        private static readonly Brush WarningFallback = CreateBrush("#E0A84C");
        private static readonly Brush StaleFallback = CreateBrush("#E08A4C");
        private static readonly Brush ErrorFallback = CreateBrush("#E05A5A");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value is SaveStatus s ? s : SaveStatus.Unknown;

            switch (status)
            {
                case SaveStatus.Confirmed:
                    return FindThemeBrush("GlyphBrush", SuccessFallback);
                case SaveStatus.Candidate:
                    return WarningFallback;
                case SaveStatus.Stale:
                    return StaleFallback;
                case SaveStatus.Error:
                    return ErrorFallback;
                default:
                    return FindThemeBrush("GlyphBrush", UnknownFallback);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }

        private static Brush FindThemeBrush(string key, Brush fallback)
        {
            try
            {
                var brush = Application.Current?.TryFindResource(key) as Brush;
                return brush ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static Brush CreateBrush(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }
    }

    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var flag = value is bool b && b;
            if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
            {
                flag = !flag;
            }
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || string.IsNullOrWhiteSpace(value as string))
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
