using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GameBackup.Core.IO;

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

            var isFile = LongPath.FileExists(savePath);
            var isDirectory = LongPath.DirectoryExists(savePath);

            if (!isFile && !isDirectory)
            {
                result.Error = "存档路径不存在：" + savePath;
                return result;
            }

            try
            {
                var gameFolder = Path.Combine(backupRoot, LudusaviMapping.EscapeFolderName(gameName));
                LongPath.CreateDirectory(gameFolder);

                var backupName = keepVersions == 1
                    ? "."
                    : "backup-" + LudusaviMapping.TimestampName(timestampUtc);
                var target = Path.Combine(gameFolder, backupName);

                if (IsInside(target, savePath))
                {
                    result.Error = "备份目录不能位于存档目录内部。";
                    return result;
                }

                var targetExisted = LongPath.DirectoryExists(target);
                LongPath.CreateDirectory(target);

                var entries = new List<MappingFileEntry>();
                var drives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var bytes = 0L;

                try
                {
                    var files = isFile
                        ? new[] { savePath }
                        : LongPath.GetFiles(savePath, SearchOption.AllDirectories);

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
                            LongPath.CreateDirectory(parent);
                        }

                        LongPath.CopyFile(file, destination, true);

                        var size = LongPath.GetFileSize(file);
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

                    var removed = Trim(document, gameFolder, keepVersions, backupName);
                    LudusaviMapping.Save(gameFolder, document);

                    foreach (var name in removed)
                    {
                        if (string.Equals(name, backupName, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        TryDelete(Path.Combine(gameFolder, name));
                    }
                }
                catch
                {
                    // A half-written version would show up as a broken entry in
                    // the history list, so drop it unless the folder existed
                    // before this run (or is the shared single-version folder).
                    if (!targetExisted && backupName != ".")
                    {
                        TryDelete(target);
                    }

                    throw;
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
        /// Drops the oldest full backups beyond the retention limit. The
        /// folders on disk are the source of truth, because the settings page
        /// lists them: a version left behind by an interrupted run has no
        /// mapping block, but it still counts towards the user's limit and
        /// must be pruned like any other. Locked backups are kept, matching
        /// Ludusavi's behaviour.
        /// </summary>
        private static List<string> Trim(
            MappingDocument document,
            string gameFolder,
            int keepVersions,
            string currentName)
        {
            var removed = new List<string>();

            if (keepVersions <= 0)
            {
                return removed;
            }

            var folders = LongPath
                .GetDirectories(gameFolder)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name)
                               && name.StartsWith("backup-", StringComparison.Ordinal)
                               && !name.EndsWith("-diff", StringComparison.Ordinal))
                .OrderByDescending(name => name, StringComparer.Ordinal)
                .ToList();

            var keep = new HashSet<string>(StringComparer.Ordinal);

            // "." is the single-version folder: it already replaces every
            // previous full backup, so none of the backup-* folders survive.
            if (currentName != ".")
            {
                keep.UnionWith(folders.Take(keepVersions));
            }

            foreach (var block in document.Backups)
            {
                if (LudusaviMapping.BlockIsLocked(block))
                {
                    keep.Add(LudusaviMapping.BlockName(block));
                }
            }

            foreach (var name in folders)
            {
                if (keep.Contains(name))
                {
                    continue;
                }

                var block = document.Backups.FirstOrDefault(
                    b => string.Equals(LudusaviMapping.BlockName(b), name, StringComparison.Ordinal));

                if (block != null)
                {
                    document.Backups.Remove(block);
                }

                removed.Add(name);
            }

            return removed;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (LongPath.DirectoryExists(path))
                {
                    LongPath.DeleteDirectory(path);
                }
            }
            catch
            {
                // A locked old version must not fail the new backup.
            }
        }

        private static bool IsInside(string candidate, string parent)
        {
            var fullCandidate = Path.GetFullPath(LongPath.FromIo(candidate));
            var fullParent = Path.GetFullPath(LongPath.FromIo(parent));
            var prefix = fullParent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
