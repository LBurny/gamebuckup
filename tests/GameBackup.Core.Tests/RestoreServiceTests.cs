using System;
using System.IO;
using GameBackup.Core.Backup;
using Xunit;

namespace GameBackup.Core.Tests
{
    public class RestoreServiceTests : IDisposable
    {
        private readonly string _root;

        public RestoreServiceTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "gb-restore-" + Guid.NewGuid().ToString("N"));
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

        private string CreateSave(out string file)
        {
            var save = Path.Combine(_root, "save");
            Directory.CreateDirectory(Path.Combine(save, "profile"));
            file = Path.Combine(save, "profile", "slot1.sav");
            File.WriteAllText(file, "original");
            File.WriteAllText(Path.Combine(save, "settings.cfg"), "cfg");
            return save;
        }

        [Fact]
        public void Restore_copies_the_latest_backup_back_to_the_original_paths()
        {
            string file;
            var save = CreateSave(out file);
            var backups = Path.Combine(_root, "backups");

            new BackupService().Backup(backups, "Test Game", save, new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc), 10);

            File.WriteAllText(file, "changed");
            File.WriteAllText(Path.Combine(save, "settings.cfg"), "changed");

            var result = new RestoreService().Restore(backups, "Test Game", null);

            Assert.True(result.Success, result.Error);
            Assert.Equal(2, result.FileCount);
            Assert.Equal("original", File.ReadAllText(file));
            Assert.Equal("cfg", File.ReadAllText(Path.Combine(save, "settings.cfg")));
        }

        [Fact]
        public void Restore_recreates_files_that_were_deleted()
        {
            string file;
            var save = CreateSave(out file);
            var backups = Path.Combine(_root, "backups");

            new BackupService().Backup(backups, "Test Game", save, new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc), 10);
            File.Delete(file);

            var result = new RestoreService().Restore(backups, "Test Game", null);

            Assert.True(result.Success, result.Error);
            Assert.True(File.Exists(file));
            Assert.Equal("original", File.ReadAllText(file));
        }

        [Fact]
        public void Restore_can_target_an_older_version()
        {
            string file;
            var save = CreateSave(out file);
            var backups = Path.Combine(_root, "backups");
            var service = new BackupService();
            service.Backup(backups, "Test Game", save, new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc), 10);

            File.WriteAllText(file, "second");
            service.Backup(backups, "Test Game", save, new DateTime(2026, 11, 8, 13, 0, 0, DateTimeKind.Utc), 10);

            var result = new RestoreService().Restore(backups, "Test Game", "backup-20261108T120000Z");

            Assert.True(result.Success, result.Error);
            Assert.Equal("original", File.ReadAllText(file));
        }

        [Fact]
        public void Restore_fails_when_no_backup_exists()
        {
            var result = new RestoreService().Restore(
                Path.Combine(_root, "backups"), "Test Game", null);

            Assert.False(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(result.Error));
        }
    }
}
