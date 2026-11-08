using GameBackup.Core;
using Xunit;

namespace GameBackup.Core.Tests
{
    public class GameDescriptorTests
    {
        [Fact]
        public void Fingerprint_changes_when_install_directory_changes()
        {
            var a = GameDescriptorFactory.Create("id", "Game", @"C:\Games\A", "Steam", "10", "PC", true);
            var b = GameDescriptorFactory.Create("id", "Game", @"C:\Games\B", "Steam", "10", "PC", true);

            Assert.NotEqual(a.Fingerprint, b.Fingerprint);
        }

        [Fact]
        public void Fingerprint_is_stable_across_case_and_separator_changes()
        {
            var a = GameDescriptorFactory.Create("id", "Game", @"C:\Games\A", "Steam", "10", "PC", true);
            var b = GameDescriptorFactory.Create("id", "Game", @"c:/games/a/", "Steam", "10", "PC", true);

            Assert.Equal(a.Fingerprint, b.Fingerprint);
        }
    }
}
