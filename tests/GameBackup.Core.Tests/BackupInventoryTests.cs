using System;
using System.IO;
using GameBackup.Core.Backup;
using Xunit;

namespace GameBackup.Core.Tests
{
    public class BackupInventoryTests : IDisposable
    {
        private readonly string _root;

        public BackupInventoryTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "gb-inv-" + Guid.NewGuid().ToString("N"));
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

        [Fact]
        public void Latest_summary_is_empty_when_nothing_was_backed_up()
        {
            var summary = BackupInventory.GetLatest(_root, "Test Game");

            Assert.False(summary.Exists);
            Assert.Equal(0, summary.FileCount);
        }

        [Fact]
        public void Latest_summary_points_at_the_newest_version()
        {
            var save = Path.Combine(_root, "save");
            Directory.CreateDirectory(save);
            File.WriteAllText(Path.Combine(save, "a.sav"), "one");
            File.WriteAllText(Path.Combine(save, "b.sav"), "two");

            var backups = Path.Combine(_root, "backups");
            var service = new BackupService();
            service.Backup(backups, "Test Game", save, new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc), 10);
            var newest = service.Backup(backups, "Test Game", save, new DateTime(2026, 11, 8, 13, 30, 0, DateTimeKind.Utc), 10);

            var summary = BackupInventory.GetLatest(backups, "Test Game");

            Assert.True(summary.Exists);
            Assert.Equal(newest.BackupDirectory, summary.Directory);
            Assert.Equal(2, summary.FileCount);
            Assert.True(summary.TotalBytes > 0);
            Assert.Equal(2026, summary.TimestampUtc.Year);
            Assert.Equal(13, summary.TimestampUtc.Hour);
        }

        [Fact]
        public void Versions_are_listed_newest_first()
        {
            var save = Path.Combine(_root, "save");
            Directory.CreateDirectory(save);
            File.WriteAllText(Path.Combine(save, "a.sav"), "one");

            var backups = Path.Combine(_root, "backups");
            var service = new BackupService();
            service.Backup(backups, "Test Game", save, new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc), 10);
            service.Backup(backups, "Test Game", save, new DateTime(2026, 11, 8, 13, 0, 0, DateTimeKind.Utc), 10);
            var newest = service.Backup(backups, "Test Game", save, new DateTime(2026, 11, 8, 14, 0, 0, DateTimeKind.Utc), 10);

            var versions = BackupInventory.GetVersions(backups, "Test Game");

            Assert.Equal(3, versions.Count);
            Assert.Equal(newest.BackupDirectory, versions[0].Directory);
            Assert.Equal(14, versions[0].TimestampUtc.Hour);
            Assert.Equal(12, versions[2].TimestampUtc.Hour);
            Assert.Equal(1, versions[0].FileCount);
        }

        [Fact]
        public void Legacy_layout_backups_are_still_listed()
        {
            var legacy = Path.Combine(_root, "backups", "Test Game", "drive-C", "backup-20261108T090000Z");
            Directory.CreateDirectory(legacy);
            File.WriteAllText(Path.Combine(legacy, "old.sav"), "old");

            var versions = BackupInventory.GetVersions(Path.Combine(_root, "backups"), "Test Game");

            Assert.Single(versions);
            Assert.Equal(9, versions[0].TimestampUtc.Hour);
        }

        [Fact]
        public void Original_save_path_is_recovered_from_the_newest_mapping_block()
        {
            var save = Path.Combine(_root, "save", "My Game");
            Directory.CreateDirectory(Path.Combine(save, "nested"));
            File.WriteAllText(Path.Combine(save, "a.sav"), "one");
            File.WriteAllText(Path.Combine(save, "nested", "b.sav"), "two");

            var backups = Path.Combine(_root, "backups");
            new BackupService().Backup(backups, "Test Game", save, new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc), 10);

            var original = BackupInventory.GetOriginalSavePath(backups, "Test Game");

            Assert.Equal(save, original);
            Assert.Null(BackupInventory.GetOriginalSavePath(backups, "Never Backed Up"));
        }

        [Fact]
        public void Newest_timestamps_cover_every_game_in_one_pass()
        {
            var save = Path.Combine(_root, "save");
            Directory.CreateDirectory(save);
            File.WriteAllText(Path.Combine(save, "a.sav"), "one");

            var backups = Path.Combine(_root, "backups");
            var service = new BackupService();
            service.Backup(backups, "Test Game", save, new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc), 10);
            service.Backup(backups, "Test Game", save, new DateTime(2026, 11, 8, 15, 0, 0, DateTimeKind.Utc), 10);
            service.Backup(backups, "Other: Game", save, new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc), 10);

            var newest = BackupInventory.GetNewestTimestamps(backups);

            Assert.Equal(2, newest.Count);
            Assert.Equal(15, newest["Test Game"].Hour);
            Assert.Equal(2026, newest["Other_ Game"].Year);
            Assert.Equal(9, newest["Other_ Game"].Hour);
        }

        [Fact]
        public void Newest_timestamps_ignore_difference_folders_and_missing_root()
        {
            var legacy = Path.Combine(_root, "backups", "Test Game", "drive-C", "backup-20261108T090000Z");
            Directory.CreateDirectory(legacy);
            var diff = Path.Combine(_root, "backups", "Test Game", "drive-C", "backup-20261108T130000Z-diff");
            Directory.CreateDirectory(diff);

            var newest = BackupInventory.GetNewestTimestamps(Path.Combine(_root, "backups"));

            Assert.Equal(9, newest["Test Game"].Hour);
            Assert.Empty(BackupInventory.GetNewestTimestamps(Path.Combine(_root, "nope")));
        }

        [Fact]
        public void Difference_backups_are_not_listed_as_full_versions()
        {
            var save = Path.Combine(_root, "save");
            Directory.CreateDirectory(save);
            File.WriteAllText(Path.Combine(save, "a.sav"), "one");

            var backups = Path.Combine(_root, "backups");
            var result = new BackupService().Backup(
                backups, "Test Game", save, new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc), 10);

            var diff = Path.Combine(Path.GetDirectoryName(result.BackupDirectory) ?? string.Empty);
            Directory.CreateDirectory(Path.Combine(diff, "backup-20261108T130000Z-diff"));

            var versions = BackupInventory.GetVersions(backups, "Test Game");

            Assert.Single(versions);
        }
    }
}
