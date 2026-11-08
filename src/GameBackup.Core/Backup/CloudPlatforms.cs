using System;
using System.Collections.Generic;
using System.Linq;

namespace GameBackup.Core.Backup
{
    /// <summary>One storefront whose own cloud saves make a local copy redundant.</summary>
    public sealed class CloudPlatform
    {
        public CloudPlatform(string key, string displayName, params string[] aliases)
        {
            Key = key;
            DisplayName = displayName;
            Aliases = aliases;
        }

        public string Key { get; }

        public string DisplayName { get; }

        public IReadOnlyList<string> Aliases { get; }

        /// <summary>Matches Playnite's source name for the store (for example "GOG Galaxy").</summary>
        public bool Matches(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                return false;
            }

            return Aliases.Any(alias => sourceName.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    /// <summary>
    /// The stores the "skip cloud-synced games" option understands. Kept in Core so
    /// the decision is unit-testable without Playnite.
    /// </summary>
    public static class CloudPlatforms
    {
        public static IReadOnlyList<CloudPlatform> All { get; } = new[]
        {
            new CloudPlatform("Epic", "Epic", "Epic"),
            new CloudPlatform("GOG", "GOG", "GOG", "Galaxy"),
            new CloudPlatform("Origin", "Origin / EA", "Origin", "EA App", "Electronic Arts"),
            new CloudPlatform("Steam", "Steam", "Steam"),
            new CloudPlatform("Ubisoft", "育碧 Uplay", "Ubisoft", "Uplay", "育碧"),
        };

        public static IReadOnlyList<string> AllKeys { get; } = All.Select(platform => platform.Key).ToArray();

        public static CloudPlatform Find(string key)
        {
            return All.FirstOrDefault(platform =>
                string.Equals(platform.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// True when backups are switched off for <paramref name="sourceName"/>
        /// because that store already syncs the saves.
        /// </summary>
        public static bool ShouldSkip(bool enabled, IEnumerable<string> selectedKeys, string sourceName)
        {
            if (!enabled || selectedKeys == null)
            {
                return false;
            }

            var selected = new HashSet<string>(
                selectedKeys.Where(key => !string.IsNullOrWhiteSpace(key)),
                StringComparer.OrdinalIgnoreCase);

            if (selected.Count == 0)
            {
                return false;
            }

            return All.Any(platform => selected.Contains(platform.Key) && platform.Matches(sourceName));
        }
    }
}
