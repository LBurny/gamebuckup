using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GameBackup.Core.Backup
{
    /// <summary>
    /// Minimal reader/writer for Ludusavi's per-game <c>mapping.yaml</c>.
    /// Existing backup blocks are carried over verbatim so backups created by
    /// Ludusavi itself (including differential/registry data) are not lost.
    /// </summary>
    public static class LudusaviMapping
    {
        public const string FileName = "mapping.yaml";

        private const string Marker = "# Written by GameBackup (Ludusavi-compatible mapping)";

        private static readonly char[] InvalidFileChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '\0' };

        /// <summary>Ludusavi escapes these characters with underscores in folder names.</summary>
        public static string EscapeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Unknown";
            }

            var builder = new StringBuilder(name.Length);
            foreach (var ch in name)
            {
                builder.Append(InvalidFileChars.Contains(ch) ? '_' : ch);
            }

            if (builder.Length > 0 && builder[0] == '.')
            {
                builder[0] = '_';
            }

            if (builder.Length > 0 && builder[builder.Length - 1] == '.')
            {
                builder[builder.Length - 1] = '_';
            }

            return builder.ToString();
        }

        /// <summary>Splits <c>C:\path</c> into <c>C:</c> and <c>path</c>.</summary>
        public static void SplitDrive(string path, out string drive, out string remainder)
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full) ?? string.Empty;

            if (root.Length >= 2 && root[1] == ':')
            {
                drive = char.ToUpperInvariant(root[0]) + ":";
                remainder = full.Substring(root.Length);
            }
            else
            {
                drive = string.Empty;
                remainder = full;
            }

            remainder = remainder.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>Folder used inside a backup for a drive, e.g. <c>drive-C</c> or <c>drive-0</c>.</summary>
        public static string DriveFolderName(string drive)
        {
            return string.IsNullOrEmpty(drive)
                ? "drive-0"
                : "drive-" + EscapeFolderName(drive.Replace(":", string.Empty));
        }

        /// <summary>Ludusavi's folder-friendly timestamp, e.g. <c>20261108T045832Z</c>.</summary>
        public static string TimestampName(DateTime timestampUtc)
        {
            return timestampUtc.ToUniversalTime()
                .ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        }

        public static string Sha1OfFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        public static MappingDocument Load(string gameFolder)
        {
            var doc = new MappingDocument();
            var path = Path.Combine(gameFolder, FileName);

            if (!File.Exists(path))
            {
                return doc;
            }

            var lines = File.ReadAllLines(path, Encoding.UTF8);
            var inBackups = false;
            var inDrives = false;
            List<string> current = null;

            foreach (var line in lines)
            {
                var trimmed = line.TrimEnd();

                if (!inBackups)
                {
                    if (trimmed.StartsWith("name:", StringComparison.Ordinal))
                    {
                        doc.Name = Unquote(trimmed.Substring("name:".Length).Trim());
                        continue;
                    }

                    if (trimmed.StartsWith("drives:", StringComparison.Ordinal))
                    {
                        inDrives = true;
                        continue;
                    }

                    if (trimmed.StartsWith("backups:", StringComparison.Ordinal))
                    {
                        inDrives = false;
                        inBackups = true;
                        continue;
                    }

                    if (inDrives && trimmed.StartsWith("  ", StringComparison.Ordinal))
                    {
                        var pair = trimmed.Trim().TrimEnd(':');
                        var index = pair.IndexOf(':');
                        if (index > 0)
                        {
                            var key = Unquote(pair.Substring(0, index).Trim());
                            var value = Unquote(pair.Substring(index + 1).Trim());
                            doc.Drives[key] = value;
                        }
                    }

                    continue;
                }

                if (trimmed.StartsWith("- ", StringComparison.Ordinal))
                {
                    current = new List<string> { trimmed };
                    doc.Backups.Add(current);
                    continue;
                }

                if (current != null && trimmed.Length > 0)
                {
                    current.Add(trimmed);
                }
            }

            return doc;
        }

        public static void Save(string gameFolder, MappingDocument doc)
        {
            Directory.CreateDirectory(gameFolder);

            var builder = new StringBuilder();
            builder.Append(Marker).Append('\n');
            builder.Append("name: ").Append(Quote(doc.Name)).Append('\n');

            if (doc.Drives.Count == 0)
            {
                builder.Append("drives: {}\n");
            }
            else
            {
                builder.Append("drives:\n");
                foreach (var drive in doc.Drives)
                {
                    builder.Append("  ").Append(QuoteKey(drive.Key)).Append(": ").Append(Quote(drive.Value)).Append('\n');
                }
            }

            builder.Append("backups:\n");
            foreach (var block in doc.Backups)
            {
                foreach (var line in block)
                {
                    builder.Append(line).Append('\n');
                }
            }

            File.WriteAllText(Path.Combine(gameFolder, FileName), builder.ToString(), new UTF8Encoding(false));
        }

        /// <summary>Builds one full-backup block in the shape Ludusavi deserializes.</summary>
        public static List<string> BuildBlock(
            string backupName,
            DateTime timestampUtc,
            IEnumerable<MappingFileEntry> files)
        {
            var lines = new List<string>
            {
                "- name: " + Quote(backupName),
                "  when: " + Quote(timestampUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)),
                "  os: windows",
            };

            var list = files.ToList();
            if (list.Count == 0)
            {
                lines.Add("  files: {}");
                return lines;
            }

            lines.Add("  files:");
            foreach (var file in list)
            {
                lines.Add("    " + Quote(file.OriginalPath) + ":");
                lines.Add("      hash: " + Quote(file.Hash));
                lines.Add("      size: " + file.Size.ToString(CultureInfo.InvariantCulture));
            }

            return lines;
        }

        public static string BlockName(IReadOnlyList<string> block)
        {
            if (block == null || block.Count == 0)
            {
                return string.Empty;
            }

            var first = block[0].TrimStart('-', ' ');
            const string prefix = "name:";
            return first.StartsWith(prefix, StringComparison.Ordinal)
                ? Unquote(first.Substring(prefix.Length).Trim())
                : string.Empty;
        }

        public static bool BlockIsLocked(IReadOnlyList<string> block)
        {
            return block != null && block.Any(x => x.Trim().Replace(" ", string.Empty) == "locked:true");
        }

        private static string Quote(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "''") + "'";
        }

        /// <summary>Leaves simple keys unquoted, matching Ludusavi's own output.</summary>
        private static string QuoteKey(string value)
        {
            if (!string.IsNullOrEmpty(value)
                && value.All(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.'))
            {
                return value;
            }

            return Quote(value);
        }

        private static string Unquote(string value)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[trimmed.Length - 1] == '\'')
            {
                return trimmed.Substring(1, trimmed.Length - 2).Replace("''", "'");
            }

            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
            {
                return trimmed.Substring(1, trimmed.Length - 2);
            }

            return trimmed;
        }
    }

    public sealed class MappingFileEntry
    {
        public MappingFileEntry(string originalPath, string hash, long size)
        {
            OriginalPath = originalPath;
            Hash = hash;
            Size = size;
        }

        public string OriginalPath { get; }

        public string Hash { get; }

        public long Size { get; }
    }

    public sealed class MappingDocument
    {
        public string Name { get; set; } = string.Empty;

        public Dictionary<string, string> Drives { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raw YAML lines per backup block, preserved as-is.</summary>
        public List<List<string>> Backups { get; } = new List<List<string>>();
    }
}
