using System;
using System.IO;
using GameBackup.Core.Backup;
using GameBackup.Core.IO;

namespace GameBackup.Core.Tests
{
    /// <summary>
    /// Builds save trees whose mirrored backup path is longer than the 260
    /// character MAX_PATH that .NET Framework (and therefore Playnite) enforces.
    /// The source side stays below the limit so the fixture itself can be
    /// created without long-path support.
    /// </summary>
    internal static class LongPathScenario
    {
        public const int MaxPath = 260;

        private static readonly string DeepFolder = Path.Combine(
            new string('a', 40),
            new string('b', 40),
            new string('c', 40),
            new string('d', 40));

        public static string CreateSave(string root)
        {
            var save = Path.Combine(root, "save");
            var file = SaveFileFor(save);

            if (file.Length >= MaxPath)
            {
                throw new InvalidOperationException(
                    "temp path is too deep to build the fixture: " + file.Length + " chars");
            }

            LongPath.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, "one");
            return save;
        }

        public static string SaveFileFor(string save)
        {
            return Path.Combine(save, DeepFolder, "slot1.sav");
        }

        /// <summary>Game name padded until the backup target exceeds MAX_PATH.</summary>
        public static string GameNameFor(string backups, string stampName, string save)
        {
            var saveFile = SaveFileFor(save);
            var game = "Test Game";

            while (BackupTarget(backups, game, stampName, saveFile).Length <= MaxPath)
            {
                game += "G";
            }

            return game;
        }

        public static string BackupTarget(string backups, string game, string stampName, string saveFile)
        {
            string drive;
            string remainder;
            LudusaviMapping.SplitDrive(saveFile, out drive, out remainder);

            return Path.Combine(backups, game, stampName, LudusaviMapping.DriveFolderName(drive), remainder);
        }
    }
}
