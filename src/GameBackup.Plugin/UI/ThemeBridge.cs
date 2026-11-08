using System.Windows;
using System.Windows.Media;

namespace GameBackup.Plugin.UI
{
    /// <summary>
    /// Playnite themes provide most of these keys, but the plugin must still render
    /// sensibly when hosted outside Playnite or with a minimal custom theme.
    /// </summary>
    public static class ThemeBridge
    {
        public static void Ensure(FrameworkElement target)
        {
            AddBrush(target, "TextBrush", "#E8E8E8");
            AddBrush(target, "TextBrushDark", "#000000");
            AddBrush(target, "ControlBackgroundBrush", "#252526");
            AddBrush(target, "WindowBaseBackgroundBrush", "#1E1E1E");
            AddBrush(target, "WindowBackgourndBrush", "#1E1E1E");
            AddBrush(target, "PanelSeparatorBrush", "#3A3A3A");
            AddBrush(target, "HighlightGlyphBrush", "#4C9AFF");
            AddBrush(target, "HoverBrush", "#22FFFFFF");
            AddBrush(target, "ButtonBackgroundBrush", "#333333");
            AddBrush(target, "ButtonIsMouseOverBackgroundBrush", "#3F3F46");
            AddBrush(target, "GlyphBrush", "#A8A8A8");
            AddBrush(target, "SidebarItemIsSelectedBackgroundBrush", "#33FFFFFF");

            if (target.TryFindResource("GB.SurfaceBrush") == null)
            {
                var surface = Application.Current?.TryFindResource("MainColorDark") as Color?;
                target.Resources["GB.SurfaceBrush"] = MakeBrush(surface ?? (Color)ColorConverter.ConvertFromString("#252526"));
            }

            if (Application.Current?.TryFindResource("ControlCornerRadius") == null)
            {
                target.Resources["ControlCornerRadius"] = new CornerRadius(4);
            }

            if (Application.Current?.TryFindResource("FontFamily") == null)
            {
                target.Resources["FontFamily"] = new FontFamily("Segoe UI");
            }

            if (Application.Current?.TryFindResource("FontSize") == null)
            {
                target.Resources["FontSize"] = 13d;
            }

            if (Application.Current?.TryFindResource("FontSizeSmall") == null)
            {
                target.Resources["FontSizeSmall"] = 11d;
            }

            if (Application.Current?.TryFindResource("FontSizeLarge") == null)
            {
                target.Resources["FontSizeLarge"] = 16d;
            }
        }

        private static void AddBrush(FrameworkElement target, string key, string fallbackColor)
        {
            if (Application.Current?.TryFindResource(key) != null)
            {
                return;
            }

            target.Resources[key] = MakeBrush((Color)ColorConverter.ConvertFromString(fallbackColor));
        }

        private static SolidColorBrush MakeBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
