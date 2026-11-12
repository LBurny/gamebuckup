using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GameBackup.Core.IO;

namespace GameBackup.Core.Backup
{
    public sealed class BackupSummary
    {
        public bool Exists { get; set; }

        public string Directory { get; set; } = string.Empty;

        public DateTime TimestampUtc { get; set; }

        public int FileCount { get; set; }

        public long TotalBytes { get; set; }
    }

    /// <summary>
    /// Reads what is already on disk so the UI can show real backup history
    /// after a restart. Understands Ludusavi's <c>backup-*</c> folders as well
    /// as the single-version <c>.</c> folder.
    /// </summary>
    public static class BackupInventory
    {
        public static BackupSummary GetLatest(string backupRoot, string gameName)
        {
            return GetVersions(backupRoot, gameName).FirstOrDefault() ?? new BackupSummary();
        }

        /// <summary>All full backups for a game, newest first.</summary>
        public static IReadOnlyList<BackupSummary> GetVersions(string backupRoot, string gameName)
        {
            var versions = new List<BackupSummary>();

            if (string.IsNullOrWhiteSpace(backupRoot) || string.IsNullOrWhiteSpace(gameName))
            {
                return versions;
            }

            try
            {
                var gameFolder = Path.Combine(backupRoot, LudusaviMapping.EscapeFolderName(gameName));
                if (!LongPath.DirectoryExists(gameFolder))
                {
                    return versions;
                }

                var folders = LongPath
                    .GetDirectories(gameFolder)
                    .Where(path =>
                    {
                        var name = Path.GetFileName(path);
                        return name == "." || (name.StartsWith("backup-", StringComparison.Ordinal)
                                               && !name.EndsWith("-diff", StringComparison.Ordinal));
                    })
                    .Concat(
                        LongPath
                            .GetDirectories(gameFolder, "drive-*")
                            .SelectMany(drive => LongPath.GetDirectories(drive, "backup-*"))
                            .Where(path => !Path.GetFileName(path).EndsWith("-diff", StringComparison.Ordinal)))
                    .Select(path => new
                    {
                        Path = path,
                        Stamp = ParseStamp(Path.GetFileName(path)),
                    })
                    .OrderByDescending(x => x.Stamp)
                    .ThenByDescending(x => x.Path, StringComparer.Ordinal)
                    .ToList();

                foreach (var folder in folders)
                {
                    var files = LongPath.GetFiles(folder.Path, SearchOption.AllDirectories);
                    versions.Add(new BackupSummary
                    {
                        Exists = true,
                        Directory = folder.Path,
                        FileCount = files.Length,
                        TotalBytes = files.Sum(LongPath.GetFileSize),
                        TimestampUtc = folder.Stamp,
                    });
                }
            }
            catch
            {
                // Unreadable backup folders are skipped rather than failing the page.
            }

            return versions;
        }

        /// <summary>
        /// Newest backup timestamp for every game folder under the backup root,
        /// keyed by the escaped folder name. Only folder names are inspected, so
        /// the page can light up every row in one pass instead of walking files
        /// per game. <see cref="DateTime.MinValue"/> means a version without a
        /// timestamp (the single-version <c>.</c> folder).
        /// </summary>
        public static IReadOnlyDictionary<string, DateTime> GetNewestTimestamps(string backupRoot)
        {
            var result = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(backupRoot) || !LongPath.DirectoryExists(backupRoot))
            {
                return result;
            }

            try
            {
                foreach (var gameFolder in LongPath.GetDirectories(backupRoot))
                {
                    var stamps = new List<DateTime>();

                    foreach (var child in LongPath.GetDirectories(gameFolder))
                    {
                        var name = Path.GetFileName(child);

                        if (name == ".")
                        {
                            stamps.Add(DateTime.MinValue);
                        }
                        else if (name.StartsWith("backup-", StringComparison.Ordinal)
                                 && !name.EndsWith("-diff", StringComparison.Ordinal))
                        {
                            stamps.Add(ParseStamp(name));
                        }
                        else if (name.StartsWith("drive-", StringComparison.Ordinal))
                        {
                            // Legacy layout kept the versions inside the drive folder.
                            try
                            {
                                stamps.AddRange(
                                    LongPath.GetDirectories(child, "backup-*")
                                        .Select(path => Path.GetFileName(path))
                                        .Where(name2 => !name2.EndsWith("-diff", StringComparison.Ordinal))
                                        .Select(ParseStamp));
                            }
                            catch
                            {
                                // A drive folder that vanished mid-scan simply adds nothing.
                            }
                        }
                    }

                    if (stamps.Count > 0)
                    {
                        result[Path.GetFileName(gameFolder)] = stamps.Max();
                    }
                }
            }
            catch
            {
                // An unreadable root leaves the page without backup badges rather than failing.
            }

            return result;
        }

        /// <summary>
        /// The folder the newest mapping block was taken from. Used when a game
        /// has no rule and the locators come up empty but a backup already
        /// exists, so restore and the exit prompt still know where to work.
        /// </summary>
        public static string GetOriginalSavePath(string backupRoot, string gameName)
        {
            if (string.IsNullOrWhiteSpace(backupRoot) || string.IsNullOrWhiteSpace(gameName))
            {
                return null;
            }

            try
            {
                var gameFolder = Path.Combine(backupRoot, LudusaviMapping.EscapeFolderName(gameName));
                var document = LudusaviMapping.Load(gameFolder);
                if (document.Backups.Count == 0)
                {
                    return null;
                }

                var paths = ExtractFilePaths(document.Backups[document.Backups.Count - 1]);
                var folders = paths
                    .Select(Path.GetDirectoryName)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return folders.Count == 0 ? null : CommonFolder(folders);
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<string> ExtractFilePaths(IReadOnlyList<string> block)
        {
            var inFiles = false;
            foreach (var raw in block)
            {
                var line = raw.TrimEnd();
                var trimmed = line.Trim();

                if (trimmed == "files:")
                {
                    inFiles = true;
                    continue;
                }

                if (!inFiles || trimmed.Length < 2 || line.Length - line.TrimStart().Length < 4)
                {
                    continue;
                }

                if (!trimmed.EndsWith(":", StringComparison.Ordinal)
                    || trimmed.StartsWith("hash:", StringComparison.Ordinal)
                    || trimmed.StartsWith("size:", StringComparison.Ordinal))
                {
                    continue;
                }

                var key = trimmed.Substring(0, trimmed.Length - 1).Trim();
                if (key.Length >= 2 && key[0] == '\'' && key[key.Length - 1] == '\'')
                {
                    key = key.Substring(1, key.Length - 2).Replace("''", "'");
                }

                if (key.Length > 0)
                {
                    yield return key;
                }
            }
        }

        private static string CommonFolder(IReadOnlyList<string> folders)
        {
            var parts = folders[0].Split(Path.DirectorySeparatorChar);
            var shared = parts.Length;

            foreach (var folder in folders.Skip(1))
            {
                var other = folder.Split(Path.DirectorySeparatorChar);
                var limit = Math.Min(shared, other.Length);
                var i = 0;
                while (i < limit && string.Equals(parts[i], other[i], StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                }

                shared = i;
            }

            return shared == 0
                ? folders[0]
                : string.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(shared));
        }

        private static DateTime ParseStamp(string folderName)        {
            if (string.Equals(folderName, ".", StringComparison.Ordinal))
            {
                return DateTime.MinValue;
            }

            var raw = (folderName ?? string.Empty).Substring("backup-".Length);
            DateTime parsed;
            if (DateTime.TryParseExact(
                    raw,
                    "yyyyMMdd'T'HHmmss'Z'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out parsed))
            {
                return parsed;
            }

            return DateTime.MinValue;
        }
    }
}
