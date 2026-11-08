using System;
using System.IO;
using System.Linq;
using GameBackup.Core.Locating;
using GameBackup.Core.Models;
using GameBackup.Core.Scanning;
using Xunit;

namespace GameBackup.Core.Tests
{
    public class PlatformLocatorTests
    {
        [Fact]
        public void Steam_userdata_is_treated_as_confirmed()
        {
            using (var tree = new TempTree())
            {
                var remote = tree.Dir("steam", "userdata", "478298662", "730", "remote");
                tree.File(remote, "csgo_save.dat");

                var environment = new SaveEnvironment();
                environment.SteamRoots.Add(tree.Dir("steam"));

                var game = Game("Counter-Strike", "Steam", "730", tree.Dir("games", "cs"));

                var result = new SaveLocator(new[] { new SteamUserDataLocator() }).Locate(game, environment);

                var best = result.Best;
                Assert.NotNull(best);
                Assert.Equal(remote, best.Path);
                Assert.Equal(CandidateSource.Steam, best.Source);
                Assert.True(best.Confidence >= SaveLocator.AutoConfirmThreshold);
                Assert.Equal(SaveStatus.Confirmed, result.Status);
            }
        }

        [Fact]
        public void Steam_app_folder_without_remote_is_only_a_candidate()
        {
            using (var tree = new TempTree())
            {
                var app = tree.Dir("steam", "userdata", "1", "999");
                tree.File(app, "player.sav");

                var environment = new SaveEnvironment();
                environment.SteamRoots.Add(tree.Dir("steam"));

                var game = Game("No Cloud Saves", "Steam", "999", string.Empty);

                var result = new SaveLocator(new[] { new SteamUserDataLocator() }).Locate(game, environment);

                Assert.NotNull(result.Best);
                Assert.Equal(app, result.Best.Path);
                Assert.Equal(CandidateSource.Steam, result.Best.Source);
                Assert.True(result.Best.Confidence < SaveLocator.AutoConfirmThreshold);
                Assert.Equal(SaveStatus.Candidate, result.Status);
            }
        }

        [Fact]
        public void Steam_remotecache_only_folder_is_ignored()
        {
            using (var tree = new TempTree())
            {
                var app = tree.Dir("steam", "userdata", "1", "999");
                tree.File(app, "remotecache.vdf");

                var environment = new SaveEnvironment();
                environment.SteamRoots.Add(tree.Dir("steam"));

                var game = Game("Cloud Only", "Steam", "999", string.Empty);

                var result = new SaveLocator(new[] { new SteamUserDataLocator() }).Locate(game, environment);

                Assert.Empty(result.Candidates);
            }
        }

        [Fact]
        public void Ubisoft_savegames_are_found_by_uplay_id()
        {
            using (var tree = new TempTree())
            {
                var savegames = tree.Dir("ubisoft", "savegames");
                var expected = tree.Dir("ubisoft", "savegames", "9f2c", "635");
                tree.File(expected, "save.dat");

                var environment = new SaveEnvironment();
                environment.UbisoftSavegameRoots.Add(savegames);

                var game = Game("Prince of Persia", "Ubisoft Connect", "635", tree.Dir("games", "pop"));

                var result = new SaveLocator(new[] { new UbisoftSavegamesLocator() }).Locate(game, environment);

                Assert.NotNull(result.Best);
                Assert.Equal(expected, result.Best.Path);
                Assert.Equal(CandidateSource.Ubisoft, result.Best.Source);
                Assert.Equal(SaveStatus.Confirmed, result.Status);
            }
        }

        [Fact]
        public void Gog_galaxy_storage_is_found_by_product_id()
        {
            using (var tree = new TempTree())
            {
                var storage = tree.Dir("gog", "Applications", "1207658924", "Storage");
                tree.File(storage, "slot1.sav");

                var environment = new SaveEnvironment();
                environment.GogApplicationRoots.Add(tree.Dir("gog", "Applications"));

                var game = Game("The Witcher 3", "GOG", "1207658924", tree.Dir("games", "witcher3"));

                var result = new SaveLocator(new[] { new GogGalaxyLocator() }).Locate(game, environment);

                Assert.NotNull(result.Best);
                Assert.Equal(storage, result.Best.Path);
                Assert.Equal(CandidateSource.Gog, result.Best.Source);
            }
        }

        [Fact]
        public void Common_root_matches_documents_my_games_by_name()
        {
            using (var tree = new TempTree())
            {
                var documents = tree.Dir("Documents");
                var expected = tree.Dir("Documents", "My Games", "Example Game", "Saves");
                tree.File(expected, "profile.sav");

                var environment = new SaveEnvironment { Documents = documents };

                var game = Game("Example Game", "GOG", string.Empty, tree.Dir("games", "example"));

                var result = new SaveLocator(new[] { new CommonSaveRootLocator() }).Locate(game, environment);

                Assert.NotNull(result.Best);
                Assert.Equal(expected, result.Best.Path);
                Assert.Equal(CandidateSource.CommonRoot, result.Best.Source);
                Assert.Equal(SaveStatus.Confirmed, result.Status);
            }
        }

        [Fact]
        public void Common_root_matches_local_low_company_product()
        {
            using (var tree = new TempTree())
            {
                var localLow = tree.Dir("AppData", "LocalLow");
                var expected = tree.Dir("AppData", "LocalLow", "ExampleStudio", "ExampleGame");
                tree.File(expected, "player.log");

                var environment = new SaveEnvironment { LocalLowAppData = localLow };

                var game = Game("Example Game", "Epic", string.Empty, tree.Dir("games", "example"));

                var result = new SaveLocator(new[] { new CommonSaveRootLocator() }).Locate(game, environment);

                Assert.NotNull(result.Best);
                Assert.Equal(expected, result.Best.Path);
                Assert.Equal(CandidateSource.CommonRoot, result.Best.Source);
            }
        }

        [Fact]
        public void Install_dir_finds_unreal_saved_save_games()
        {
            using (var tree = new TempTree())
            {
                var install = tree.Dir("games", "MyGame");
                var expected = tree.Dir("games", "MyGame", "MyGame", "Saved", "SaveGames");
                tree.File(expected, "save1.sav");

                var environment = new SaveEnvironment();
                var game = Game("My Game", string.Empty, string.Empty, install);

                var result = new SaveLocator(new[] { new InstallDirectoryLocator() }).Locate(game, environment);

                Assert.NotNull(result.Best);
                Assert.Equal(expected, result.Best.Path);
                Assert.Equal(CandidateSource.InstallDir, result.Best.Source);
                Assert.Equal(SaveStatus.Confirmed, result.Status);
            }
        }

        [Fact]
        public void Duplicate_paths_are_merged_once()
        {
            using (var tree = new TempTree())
            {
                var documents = tree.Dir("Documents");
                var expected = tree.Dir("Documents", "My Games", "My Game", "saves");
                tree.File(expected, "a.sav");

                var environment = new SaveEnvironment { Documents = documents };
                var game = Game("My Game", string.Empty, string.Empty, tree.Dir("install", "MyGame"));

                var result = new SaveLocator(new ISaveLocator[]
                {
                    new CommonSaveRootLocator(),
                    new CommonSaveRootLocator(),
                }).Locate(game, environment);

                Assert.Single(result.Candidates, x => x.Path == expected);
            }
        }

        private static GameDescriptor Game(string name, string source, string storeId, string install)
        {
            return new GameDescriptor
            {
                Key = "test:" + name,
                Name = name,
                Source = source,
                StoreGameId = storeId,
                InstallDirectory = install,
                IsInstalled = true,
            };
        }

        private sealed class TempTree : IDisposable
        {
            private readonly string _root;

            public TempTree()
            {
                _root = Path.Combine(Path.GetTempPath(), "gb-tests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_root);
            }

            public string Dir(params string[] parts)
            {
                var path = Path.Combine(new[] { _root }.Concat(parts).ToArray());
                Directory.CreateDirectory(path);
                return path;
            }

            public void File(string directory, string name)
            {
                System.IO.File.WriteAllText(Path.Combine(directory, name), "test");
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(_root, true);
                }
                catch
                {
                    // Best effort cleanup.
                }
            }
        }
    }
}
