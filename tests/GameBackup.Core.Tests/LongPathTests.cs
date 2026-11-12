using System;
using System.IO;
using System.Text;
using GameBackup.Core.IO;
using Xunit;

namespace GameBackup.Core.Tests
{
    public class LongPathTests : IDisposable
    {
        private readonly string _root;

        public LongPathTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "gb-long-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try
            {
                if (LongPath.DirectoryExists(_root))
                {
                    LongPath.DeleteDirectory(_root);
                }
            }
            catch
            {
            }
        }

        [Fact]
        public void ForIo_adds_the_device_prefix_to_drive_paths()
        {
            Assert.Equal(@"\\?\C:\save\slot.sav", LongPath.ForIo(@"C:\save\slot.sav"));
        }

        [Fact]
        public void ForIo_leaves_already_prefixed_paths_alone()
        {
            Assert.Equal(@"\\?\C:\save", LongPath.ForIo(@"\\?\C:\save"));
        }

        [Fact]
        public void ForIo_uses_the_unc_form_for_network_paths()
        {
            Assert.Equal(@"\\?\UNC\server\share\save", LongPath.ForIo(@"\\server\share\save"));
        }

        [Fact]
        public void FromIo_returns_plain_paths()
        {
            Assert.Equal(@"C:\save", LongPath.FromIo(@"\\?\C:\save"));
            Assert.Equal(@"\\server\share\save", LongPath.FromIo(@"\\?\UNC\server\share\save"));
            Assert.Equal(@"C:\save", LongPath.FromIo(@"C:\save"));
        }

        [Fact]
        public void File_operations_work_beyond_max_path()
        {
            var file = Path.Combine(
                _root,
                new string('a', 80),
                new string('b', 80),
                new string('c', 80),
                "save.sav");

            Assert.True(file.Length > LongPathScenario.MaxPath);

            LongPath.CreateDirectory(Path.GetDirectoryName(file));
            LongPath.WriteAllText(file, "data", new UTF8Encoding(false));

            Assert.True(LongPath.FileExists(file));
            Assert.Equal(4, LongPath.GetFileSize(file));
            Assert.Equal("data", File.ReadAllText(LongPath.ForIo(file)));
        }
    }
}
