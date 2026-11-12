using System;
using System.IO;
using System.Linq;
using GameBackup.Core.Backup;
using GameBackup.Core.IO;
using Xunit;

namespace GameBackup.Core.Tests
{
    public class BackupServiceTests : IDisposable
    {
        private readonly string _root;

        public BackupServiceTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "gb-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch
            {
            }
        }

        private string CreateSaveFolder()
        {
            var save = Path.Combine(_root, "save");
            Directory.CreateDirectory(Path.Combine(save, "profile"));
            File.WriteAllText(Path.Combine(save, "slot1.sav"), "one");
            File.WriteAllText(Path.Combine(save, "profile", "settings.cfg"), "two");
            return save;
        }

        private static string InsideBackup(BackupResult result, string originalPath)
        {
            string drive;
            string remainder;
            LudusaviMapping.SplitDrive(originalPath, out drive, out remainder);
            return Path.Combine(result.BackupDirectory, LudusaviMapping.DriveFolderName(drive), remainder);
        }

        [Fact]
        public void Backup_uses_ludusavi_folder_layout()
        {
            var save = CreateSaveFolder();
            var backups = Path.Combine(_root, "backups");

            var result = new BackupService().Backup(
                backups, "Test Game", save, new DateTime(2026, 11, 8, 12, 34, 56, DateTimeKind.Utc), 10);

            Assert.True(result.Success, result.Error);
            Assert.Equal(2, result.FileCount);
            Assert.Equal(
                Path.Combine(backups, "Test Game", "backup-20261108T123456Z"),
                result.BackupDirectory);
            Assert.True(File.Exists(InsideBackup(result, Path.Combine(save, "slot1.sav"))));
            Assert.True(File.Exists(InsideBackup(result, Path.Combine(save, "profile", "settings.cfg"))));
        }

        [Fact]
        public void Backup_records_ludusavi_mapping_entries()
        {
            var save = CreateSaveFolder();
            var backups = Path.Combine(_root, "backups");

            var result = new BackupService().Backup(
                backups, "Test Game", save, new DateTime(2026, 11, 8, 12, 34, 56, DateTimeKind.Utc), 10);

            Assert.True(result.Success, result.Error);

            var mapping = File.ReadAllText(Path.Combine(backups, "Test Game", "mapping.yaml"));
            Assert.Contains("name: 'Test Game'", mapping);
            Assert.Contains("drive-C:", mapping);
            Assert.Contains("name: 'backup-20261108T123456Z'", mapping);
            Assert.Contains("when: '2026-11-08T12:34:56Z'", mapping);
            Assert.Contains("os: windows", mapping);
            Assert.Contains(Path.Combine(save, "slot1.sav"), mapping.Replace("''", "'"));
            Assert.Contains("hash:", mapping);
            Assert.Contains("size: 3", mapping);
        }

        [Fact]
        public void Backup_keeps_previous_mapping_entries_verbatim()
        {
            var save = CreateSaveFolder();
            var backups = Path.Combine(_root, "backups");
            var gameFolder = Path.Combine(backups, "Test Game");
            Directory.CreateDirectory(gameFolder);

            File.WriteAllText(
                Path.Combine(gameFolder, "mapping.yaml"),
                "name: Test Game\n" +
                "drives:\n" +
                "  drive-C: 'C:'\n" +
                "backups:\n" +
                "- name: backup-20200101T000000Z\n" +
                "  when: 2020-01-01T00:00:00Z\n" +
                "  os: windows\n" +
                "  files:\n" +
                "    'C:/old.sav':\n" +
                "      hash: abc\n" +
                "      size: 1\n");

            var result = new BackupService().Backup(
                backups, "Test Game", save, new DateTime(2026, 11, 8, 12, 34, 56, DateTimeKind.Utc), 10);

            Assert.True(result.Success, result.Error);

            var mapping = File.ReadAllText(Path.Combine(gameFolder, "mapping.yaml"));
            Assert.Contains("backup-20200101T000000Z", mapping);
            Assert.Contains("hash: abc", mapping);
            Assert.Contains("backup-20261108T123456Z", mapping);
        }

        [Fact]
        public void Backup_prunes_older_versions_beyond_the_retention_limit()
        {
            var save = CreateSaveFolder();
            var backups = Path.Combine(_root, "backups");
            var service = new BackupService();
            var start = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

            for (var i = 0; i < 12; i++)
            {
                var result = service.Backup(backups, "Test Game", save, start.AddMinutes(i), 10);
                Assert.True(result.Success, result.Error);
            }

            var versions = Directory
                .GetDirectories(Path.Combine(backups, "Test Game"), "backup-*")
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(10, versions.Count);
            Assert.Contains("backup-20261108T121100Z", versions.Last());
            Assert.DoesNotContain("backup-20261108T120000Z", versions);

            var mapping = File.ReadAllText(Path.Combine(backups, "Test Game", "mapping.yaml"));
            Assert.DoesNotContain("backup-20261108T120000Z", mapping);
        }

        [Fact]
        public void Backup_prunes_versions_that_have_no_mapping_block()
        {
            var save = CreateSaveFolder();
            var backups = Path.Combine(_root, "backups");
            var gameFolder = Path.Combine(backups, "Test Game");
            Directory.CreateDirectory(gameFolder);

            // Interrupted runs used to leave folders behind without a mapping
            // block; they still show up in the history list and must count
            // towards the retention limit.
            for (var day = 0; day < 5; day++)
            {
                var orphan = Path.Combine(gameFolder, "backup-2026110" + day + "T000000Z");
                Directory.CreateDirectory(orphan);
                File.WriteAllText(Path.Combine(orphan, "slot1.sav"), "orphan");
            }

            var result = new BackupService().Backup(
                backups, "Test Game", save, new DateTime(2026, 11, 8, 12, 34, 56, DateTimeKind.Utc), 5);

            Assert.True(result.Success, result.Error);

            var versions = Directory
                .GetDirectories(gameFolder, "backup-*")
                .Select(Path.GetFileName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(5, versions.Count);
            Assert.DoesNotContain("backup-20261100T000000Z", versions);
            Assert.DoesNotContain(
                "backup-20261100T000000Z",
                File.ReadAllText(Path.Combine(gameFolder, "mapping.yaml")));
        }

        [Fact]
        public void Single_version_mode_removes_older_full_backups()
        {
            var save = CreateSaveFolder();
            var backups = Path.Combine(_root, "backups");
            var gameFolder = Path.Combine(backups, "Test Game");
            var service = new BackupService();

            service.Backup(backups, "Test Game", save, new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc), 10);
            service.Backup(backups, "Test Game", save, new DateTime(2026, 11, 8, 13, 0, 0, DateTimeKind.Utc), 10);

            var result = service.Backup(backups, "Test Game", save, new DateTime(2026, 11, 8, 14, 0, 0, DateTimeKind.Utc), 1);

            Assert.True(result.Success, result.Error);
            Assert.Equal(Path.Combine(gameFolder, "."), result.BackupDirectory);
            Assert.Empty(Directory.GetDirectories(gameFolder, "backup-*"));

            var mapping = File.ReadAllText(Path.Combine(gameFolder, "mapping.yaml"));
            Assert.DoesNotContain("backup-20261108T120000Z", mapping);
            Assert.Contains("name: '.'", mapping);
        }

        [Fact]
        public void Backup_fails_when_the_save_path_is_missing()
        {
            var result = new BackupService().Backup(
                Path.Combine(_root, "backups"),
                "Test Game",
                Path.Combine(_root, "does-not-exist"),
                DateTime.UtcNow,
                10);

            Assert.False(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(result.Error));
        }

        [Fact]
        public void Backup_copies_a_single_save_file()
        {
            var saveFolder = CreateSaveFolder();
            var file = Path.Combine(saveFolder, "slot1.sav");
            var backups = Path.Combine(_root, "backups");

            var result = new BackupService().Backup(
                backups, "Test Game", file, new DateTime(2026, 11, 8, 12, 34, 56, DateTimeKind.Utc), 10);

            Assert.True(result.Success, result.Error);
            Assert.Equal(1, result.FileCount);
            Assert.True(File.Exists(InsideBackup(result, file)));
        }

        [Fact]
        public void Escape_folder_name_replaces_invalid_characters()
        {
            Assert.Equal("Onimusha_ Way of the Sword", LudusaviMapping.EscapeFolderName("Onimusha: Way of the Sword"));
            Assert.Equal("_hidden", LudusaviMapping.EscapeFolderName(".hidden"));
        }

        [Fact]
        public void Backup_copies_files_whose_backup_path_exceeds_max_path()
        {
            var backups = Path.Combine(_root, "backups");
            var save = LongPathScenario.CreateSave(_root);
            var when = new DateTime(2026, 11, 8, 12, 34, 56, DateTimeKind.Utc);
            var stamp = "backup-" + LudusaviMapping.TimestampName(when);
            var game = LongPathScenario.GameNameFor(backups, stamp, save);
            var target = LongPathScenario.BackupTarget(
                backups, game, stamp, LongPathScenario.SaveFileFor(save));

            Assert.True(target.Length > LongPathScenario.MaxPath);

            var result = new BackupService().Backup(backups, game, save, when, 10);

            Assert.True(result.Success, result.Error);
            Assert.Equal(1, result.FileCount);
            Assert.True(LongPath.FileExists(target));
            Assert.Equal("one", File.ReadAllText(LongPath.ForIo(target)));

            var mapping = File.ReadAllText(Path.Combine(backups, game, "mapping.yaml"));
            Assert.Contains(LongPathScenario.SaveFileFor(save), mapping);
            Assert.DoesNotContain(@"\\?\", mapping);
        }

        [Fact]
        public void Backup_discards_the_half_written_version_when_a_file_cannot_be_copied()
        {
            var save = CreateSaveFolder();
            var backups = Path.Combine(_root, "backups");
            var locked = Path.Combine(save, "locked.sav");
            File.WriteAllText(locked, "locked");

            BackupResult result;
            using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                result = new BackupService().Backup(
                    backups,
                    "Test Game",
                    save,
                    new DateTime(2026, 11, 8, 12, 34, 56, DateTimeKind.Utc),
                    10);
            }

            Assert.False(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(result.Error));
            Assert.False(Directory.Exists(Path.Combine(backups, "Test Game", "backup-20261108T123456Z")));
        }
    }
}
