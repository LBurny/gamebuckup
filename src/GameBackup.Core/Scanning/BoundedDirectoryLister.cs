using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameBackup.Core.Scanning
{
    /// <summary>
    /// Lists a directory tree with hard limits so that untrusted or enormous
    /// game folders can never stall the UI or blow up the prompt.
    /// </summary>
    public static class BoundedDirectoryLister
    {
        private static readonly HashSet<string> DefaultExcluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules",
            ".git",
            ".svn",
            "__pycache__",
            "cache",
            "caches",
            "logs",
            "log",
            "temp",
            "tmp",
            "crashdumps",
            "shadercache",
            "dxcache",
            "gpucache",
        };

        public static IReadOnlyList<string> List(
            string root,
            int maxDepth,
            int maxEntries,
            ISet<string> excludedNames = null)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return results;
            }

            var excluded = excludedNames ?? DefaultExcluded;
            Walk(root, root, 0, maxDepth, maxEntries, excluded, results);
            return results;
        }

        private static void Walk(
            string root,
            string current,
            int depth,
            int maxDepth,
            int maxEntries,
            ISet<string> excluded,
            List<string> results)
        {
            if (results.Count >= maxEntries)
            {
                return;
            }

            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(current)
                    .OrderBy(x => Directory.Exists(x) ? 0 : 1)
                    .ThenBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (results.Count >= maxEntries)
                {
                    return;
                }

                var name = Path.GetFileName(entry);
                if (string.IsNullOrEmpty(name) || excluded.Contains(name))
                {
                    continue;
                }

                bool isDirectory;
                try
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                    isDirectory = (attributes & FileAttributes.Directory) != 0;
                }
                catch
                {
                    continue;
                }

                var relative = GetRelativePath(root, entry).Replace('\\', '/');
                results.Add(isDirectory ? relative + "/" : relative);

                if (isDirectory && depth < maxDepth)
                {
                    Walk(root, entry, depth + 1, maxDepth, maxEntries, excluded, results);
                }
            }
        }

        private static string GetRelativePath(string root, string path)
        {
            var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                 + Path.DirectorySeparatorChar;
            if (path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return path.Substring(normalizedRoot.Length);
            }
            return Path.GetFileName(path);
        }
    }
}
