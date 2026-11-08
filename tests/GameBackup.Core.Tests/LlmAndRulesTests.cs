using System;
using System.IO;
using GameBackup.Core;
using GameBackup.Core.Llm;
using GameBackup.Core.Rules;
using Xunit;

namespace GameBackup.Core.Tests
{
    public class LlmAndRulesTests
    {
        [Fact]
        public void Rule_store_round_trips_a_confirmed_location()
        {
            var directory = Path.Combine(Path.GetTempPath(), "GameBackupTests_" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "rules.json");

            try
            {
                var store = new RuleStore(path);
                store.Upsert(new GameRule
                {
                    Key = "playnite:abc",
                    Name = "Example",
                    AppId = "123",
                    InstallDir = @"C:\Games\Example",
                    Saves = new[] { @"C:\Users\Test\AppData\Local\Example\Saves" },
                    Source = "llm+confirmed",
                    ConfirmedAt = "2026-11-08T00:00:00Z",
                });

                var found = store.Find("playnite:abc");

                Assert.NotNull(found);
                Assert.Equal("Example", found.Name);
                Assert.Single(found.Saves);
                Assert.Equal(@"C:\Users\Test\AppData\Local\Example\Saves", found.Saves[0]);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Fact]
        public void Context_redacts_home_and_includes_install_listing()
        {
            var root = Path.Combine(Path.GetTempPath(), "GameBackupCtx_" + Guid.NewGuid().ToString("N"));
            var home = Path.Combine(root, "home");
            var install = Path.Combine(root, "install");

            try
            {
                Directory.CreateDirectory(Path.Combine(home, "Documents", "My Games", "Example", "Saves"));
                Directory.CreateDirectory(Path.Combine(install, "data"));
                File.WriteAllText(Path.Combine(install, "Example.exe"), "x");

                var game = GameDescriptorFactory.Create(
                    "playnite:abc", "Example", install, "Steam", "123", "PC", true);

                var context = LlmContextBuilder.Build(game, home);

                Assert.DoesNotContain(home, context.UserPrompt);
                Assert.Contains("<home>", context.UserPrompt);
                Assert.Contains("Example.exe", context.UserPrompt);
                Assert.Contains("Save root", context.UserPrompt);
                Assert.Contains("Example/Saves", context.UserPrompt);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Fact]
        public void Bounded_lister_stops_at_the_entry_limit()
        {
            var root = Path.Combine(Path.GetTempPath(), "GameBackupScan_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(root);
                for (var i = 0; i < 20; i++)
                {
                    File.WriteAllText(Path.Combine(root, "file" + i + ".txt"), "x");
                }

                var entries = GameBackup.Core.Scanning.BoundedDirectoryLister.List(root, 1, 5);

                Assert.Equal(5, entries.Count);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }
    }
}
