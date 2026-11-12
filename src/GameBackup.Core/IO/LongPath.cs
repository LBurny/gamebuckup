using System;
using System.IO;
using System.Text;

namespace GameBackup.Core.IO
{
    /// <summary>
    /// Playnite hosts plugins on .NET Framework, which still enforces the classic
    /// 260 character <c>MAX_PATH</c> limit unless a path carries the <c>\\?\</c>
    /// prefix. Backup destinations mirror the whole save path (plus the game
    /// folder, timestamp and <c>drive-C</c>), so a deep save folder easily
    /// produces a destination the runtime refuses to copy to.
    ///
    /// Every path in this class is converted only at the moment it reaches the
    /// file APIs, so callers keep working with normal, Ludusavi-compatible paths.
    /// </summary>
    public static class LongPath
    {
        private const string DevicePrefix = @"\\?\";
        private const string UncPrefix = @"\\?\UNC\";

        /// <summary>Absolute path suitable for the Win32 file APIs.</summary>
        public static string ForIo(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (path.StartsWith(DevicePrefix, StringComparison.Ordinal))
            {
                return path;
            }

            var full = Path.GetFullPath(path);

            if (full.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return UncPrefix + full.Substring(2);
            }

            if (full.Length >= 2 && full[1] == ':')
            {
                return DevicePrefix + full;
            }

            // Drive-relative or device paths are left untouched.
            return full;
        }

        /// <summary>Turns a path returned by the file APIs back into its plain form.</summary>
        public static string FromIo(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (path.StartsWith(UncPrefix, StringComparison.Ordinal))
            {
                return @"\\" + path.Substring(UncPrefix.Length);
            }

            if (path.StartsWith(DevicePrefix, StringComparison.Ordinal))
            {
                return path.Substring(DevicePrefix.Length);
            }

            return path;
        }

        public static bool FileExists(string path)
        {
            return File.Exists(ForIo(path));
        }

        public static bool DirectoryExists(string path)
        {
            return Directory.Exists(ForIo(path));
        }

        public static void CreateDirectory(string path)
        {
            Directory.CreateDirectory(ForIo(path));
        }

        public static void CopyFile(string source, string destination, bool overwrite)
        {
            File.Copy(ForIo(source), ForIo(destination), overwrite);
        }

        public static void DeleteDirectory(string path)
        {
            Directory.Delete(ForIo(path), true);
        }

        public static long GetFileSize(string path)
        {
            return new FileInfo(ForIo(path)).Length;
        }

        public static DateTime GetLastWriteTimeUtc(string path)
        {
            return File.GetLastWriteTimeUtc(ForIo(path));
        }

        public static FileAttributes GetAttributes(string path)
        {
            return File.GetAttributes(ForIo(path));
        }

        public static FileStream OpenRead(string path)
        {
            return File.OpenRead(ForIo(path));
        }

        /// <summary>All file paths in their plain form, even below MAX_PATH.</summary>
        public static string[] GetFiles(string path, SearchOption searchOption)
        {
            return FromIo(Directory.GetFiles(ForIo(path), "*", searchOption));
        }

        public static string[] GetDirectories(string path)
        {
            return FromIo(Directory.GetDirectories(ForIo(path)));
        }

        public static string[] GetDirectories(string path, string searchPattern)
        {
            return FromIo(Directory.GetDirectories(ForIo(path), searchPattern));
        }

        public static string[] ReadAllLines(string path, Encoding encoding)
        {
            return File.ReadAllLines(ForIo(path), encoding);
        }

        public static void WriteAllText(string path, string contents, Encoding encoding)
        {
            File.WriteAllText(ForIo(path), contents, encoding);
        }

        private static string[] FromIo(string[] paths)
        {
            for (var i = 0; i < paths.Length; i++)
            {
                paths[i] = FromIo(paths[i]);
            }

            return paths;
        }
    }
}
