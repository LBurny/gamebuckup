using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameBackup.Plugin.Adapters
{
    public static class ImageLoader
    {
        /// <summary>
        /// Loads and freezes a thumbnail so the file is never locked and the image
        /// can be used from any thread.
        /// </summary>
        public static ImageSource Load(string path, int decodeWidth)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.DecodePixelWidth = decodeWidth;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
