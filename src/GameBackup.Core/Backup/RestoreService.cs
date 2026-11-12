using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameBackup.Core.IO;

namespace GameBackup.Core.Backup
{
    public sealed class RestoreResult
    {
        public bool Success { get; set; }

        public string BackupDirectory { get; set; } = string.Empty;

        public int FileCount { get; set; }

        public long TotalBytes { get; set; }

        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// Copies a Ludusavi-layout backup back to the original locations.
    /// Files under <c>backup-X\drive-C\...</c> return to <c>C:\...</c>.
    /// </summary>
    public sealed class RestoreService
    {
        public RestoreResult Restore(string backupRoot, string gameName, string backupName)
        {
            var result = new RestoreResult();

            if (string.IsNullOrWhiteSpace(backupRoot) || string.IsNullOrWhiteSpace(gameName))
            {
                result.Error = "尚未设置备份目录。";
                return result;
            }

            try
            {
                var gameFolder = Path.Combine(backupRoot, LudusaviMapping.EscapeFolderName(gameName));
                var source = ResolveBackup(gameFolder, backupName);

                if (source == null)
                {
                    result.Error = "没有可用于恢复的备份。";
                    return result;
                }

                var files = LongPath.GetFiles(source, SearchOption.AllDirectories);
                var bytes = 0L;

                foreach (var file in files)
                {
                    var relative = file.Substring(source.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    var separator = relative.IndexOf(Path.DirectorySeparatorChar);
                    if (separator <= 0)
                    {
                        continue;
                    }

                    var driveFolder = relative.Substring(0, separator);
                    var remainder = relative.Substring(separator + 1);

                    if (!driveFolder.StartsWith("drive-", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var drive = driveFolder.Substring("drive-".Length);
                    string destination;

                    if (drive == "0")
                    {
                        destination = remainder;
                    }
                    else if (drive.Length == 1)
                    {
                        destination = drive + ":" + Path.DirectorySeparatorChar + remainder;
                    }
                    else
                    {
                        continue;
                    }

                    var parent = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        LongPath.CreateDirectory(parent);
                    }

                    LongPath.CopyFile(file, destination, true);
                    bytes += LongPath.GetFileSize(file);
                    result.FileCount++;
                }

                result.Success = true;
                result.BackupDirectory = source;
                result.TotalBytes = bytes;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
        }

        private static string ResolveBackup(string gameFolder, string backupName)
        {
            if (!LongPath.DirectoryExists(gameFolder))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(backupName))
            {
                var named = Path.Combine(gameFolder, backupName);
                return LongPath.DirectoryExists(named) ? named : null;
            }

            var candidates = new List<string>();

            candidates.AddRange(
                LongPath.GetDirectories(gameFolder)
                    .Where(path =>
                    {
                        var name = Path.GetFileName(path);
                        return name == "." || (name.StartsWith("backup-", StringComparison.Ordinal)
                                               && !name.EndsWith("-diff", StringComparison.Ordinal));
                    }));

            // Older GameBackup builds stored drive folders above the backup folder.
            candidates.AddRange(
                LongPath.GetDirectories(gameFolder, "drive-*")
                    .SelectMany(drive => LongPath.GetDirectories(drive, "backup-*")));

            return candidates
                .OrderByDescending(x => Path.GetFileName(x), StringComparer.Ordinal)
                .FirstOrDefault();
        }
    }
}
