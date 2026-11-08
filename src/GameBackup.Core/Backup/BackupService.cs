using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GameBackup.Core.Backup
{
    /// <summary>Outcome of a single backup run.</summary>
    public sealed class BackupResult
    {
        public bool Success { get; set; }

        public string BackupDirectory { get; set; } = string.Empty;

        public int FileCount { get; set; }

        public long TotalBytes { get; set; }

        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// Full (non-differential) backup using Ludusavi's on-disk layout:
    /// <code>
    /// &lt;root&gt;\&lt;game&gt;\mapping.yaml
    /// &lt;root&gt;\&lt;game&gt;\backup-20261108T045832Z\drive-C\Users\...\save.sav
    /// </code>
    /// Files keep their full original path minus the drive, so Ludusavi (or this
    /// plugin later) can restore them without guessing.
    /// </summary>
    public sealed class BackupService
    {
        public BackupResult Backup(
            string backupRoot,
            string gameName,
            string savePath,
            DateTime timestampUtc,
            int keepVersions)
        {
            var result = new BackupResult();

            if (string.IsNullOrWhiteSpace(backupRoot))
            {
                result.Error = "尚未设置备份目录。";
                return result;
            }

            if (string.IsNullOrWhiteSpace(savePath))
            {
                result.Error = "这款游戏还没有存档路径。";
                return result;
            }

            var isFile = File.Exists(savePath);
            var isDirectory = Directory.Exists(savePath);

            if (!isFile && !isDirectory)
            {
                result.Error = "存档路径不存在：" + savePath;
                return result;
            }

            try
            {
                var gameFolder = Path.Combine(backupRoot, LudusaviMapping.EscapeFolderName(gameName));
                Directory.CreateDirectory(gameFolder);

                var backupName = keepVersions == 1
                    ? "."
                    : "backup-" + LudusaviMapping.TimestampName(timestampUtc);
                var target = Path.Combine(gameFolder, backupName);

                if (IsInside(target, savePath))
                {
                    result.Error = "备份目录不能位于存档目录内部。";
                    return result;
                }

                Directory.CreateDirectory(target);

                var files = isFile
                    ? new[] { savePath }
                    : Directory.GetFiles(savePath, "*", SearchOption.AllDirectories);

                var entries = new List<MappingFileEntry>();
                var drives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var bytes = 0L;

                foreach (var file in files)
                {
                    string drive;
                    string remainder;
                    LudusaviMapping.SplitDrive(file, out drive, out remainder);

                    drives.Add(drive);

                    var destination = Path.Combine(
                        target,
                        LudusaviMapping.DriveFolderName(drive),
                        remainder);

                    var parent = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    File.Copy(file, destination, true);

                    var size = new FileInfo(file).Length;
                    bytes += size;
                    entries.Add(new MappingFileEntry(file, LudusaviMapping.Sha1OfFile(file), size));
                }

                var document = LudusaviMapping.Load(gameFolder);
                document.Name = gameName;

                foreach (var drive in drives)
                {
                    document.Drives[LudusaviMapping.DriveFolderName(drive)] = drive;
                }

                document.Backups.RemoveAll(
                    block => string.Equals(
                        LudusaviMapping.BlockName(block),
                        backupName,
                        StringComparison.Ordinal));

                document.Backups.Add(
                    LudusaviMapping.BuildBlock(backupName, timestampUtc, entries));

                var removed = Trim(document, keepVersions);
                LudusaviMapping.Save(gameFolder, document);

                foreach (var name in removed)
                {
                    if (string.Equals(name, backupName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    TryDelete(Path.Combine(gameFolder, name));
                }

                result.Success = true;
                result.BackupDirectory = target;
                result.FileCount = entries.Count;
                result.TotalBytes = bytes;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Drops the oldest full backups beyond the retention limit. Locked
        /// backups are kept, matching Ludusavi's behaviour.
        /// </summary>
        private static List<string> Trim(MappingDocument document, int keepVersions)
        {
            var removed = new List<string>();

            if (keepVersions <= 0)
            {
                return removed;
            }

            var fulls = document.Backups
                .Select(block => new { Block = block, Name = LudusaviMapping.BlockName(block) })
                .Where(x => !string.IsNullOrEmpty(x.Name) && x.Name != ".")
                .OrderByDescending(x => x.Name, StringComparer.Ordinal)
                .ToList();

            var keep = fulls.Take(keepVersions).Select(x => x.Name).ToList();

            foreach (var full in fulls)
            {
                if (keep.Contains(full.Name, StringComparer.Ordinal))
                {
                    continue;
                }

                if (LudusaviMapping.BlockIsLocked(full.Block))
                {
                    continue;
                }

                document.Backups.Remove(full.Block);
                removed.Add(full.Name);
            }

            return removed;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // A locked old version must not fail the new backup.
            }
        }

        private static bool IsInside(string candidate, string parent)
        {
            var fullCandidate = Path.GetFullPath(candidate);
            var fullParent = Path.GetFullPath(parent);
            var prefix = fullParent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
