using System.Linq;
using GameBackup.Core.Backup;
using Xunit;

namespace GameBackup.Core.Tests
{
    public class CloudPlatformsTests
    {
        [Theory]
        [InlineData("Steam", "Steam")]
        [InlineData("steam", "Steam")]
        [InlineData("GOG Galaxy", "GOG")]
        [InlineData("Epic", "Epic")]
        [InlineData("EA app", "Origin")]
        [InlineData("Origin", "Origin")]
        [InlineData("Ubisoft Connect", "Ubisoft")]
        [InlineData("育碧 Uplay", "Ubisoft")]
        public void Known_source_names_resolve_to_their_platform(string sourceName, string expectedKey)
        {
            var platform = CloudPlatforms.All.Single(x => x.Matches(sourceName));

            Assert.Equal(expectedKey, platform.Key);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("手动导入")]
        [InlineData("Dream Games")] // "ea" must not match inside another word
        public void Unrelated_source_names_do_not_match(string sourceName)
        {
            Assert.DoesNotContain(CloudPlatforms.All, platform => platform.Matches(sourceName));
        }

        [Fact]
        public void Five_platforms_are_offered()
        {
            Assert.Equal(5, CloudPlatforms.All.Count);
            Assert.Equal(
                new[] { "Epic", "GOG", "Origin", "Steam", "Ubisoft" },
                CloudPlatforms.AllKeys.ToArray());
        }

        [Fact]
        public void Skip_is_off_until_the_option_is_enabled()
        {
            Assert.False(CloudPlatforms.ShouldSkip(false, CloudPlatforms.AllKeys, "Steam"));
        }

        [Fact]
        public void Skip_needs_at_least_one_selected_platform()
        {
            Assert.False(CloudPlatforms.ShouldSkip(true, Enumerable.Empty<string>(), "Steam"));
            Assert.False(CloudPlatforms.ShouldSkip(true, null, "Steam"));
        }

        [Fact]
        public void Skip_applies_only_to_the_selected_platforms()
        {
            Assert.True(CloudPlatforms.ShouldSkip(true, new[] { "Steam" }, "Steam"));
            Assert.False(CloudPlatforms.ShouldSkip(true, new[] { "Steam" }, "GOG Galaxy"));
            Assert.False(CloudPlatforms.ShouldSkip(true, new[] { "Steam" }, "手动导入"));
        }

        [Fact]
        public void Selected_keys_are_case_insensitive()
        {
            Assert.True(CloudPlatforms.ShouldSkip(true, new[] { "steam" }, "Steam"));
        }
    }
}
