using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameBackup.Core.Models;
using GameBackup.Core.Scanning;

namespace GameBackup.Core.Locating
{
    /// <summary>
    /// Runs the deterministic locators in trust order, merges duplicates and
    /// marks whether the best hit is good enough to auto-confirm.
    /// </summary>
    public sealed class SaveLocator
    {
        /// <summary>At or above this confidence an existing path is used without asking.</summary>
        public const double AutoConfirmThreshold = 0.85;

        private readonly List<ISaveLocator> _locators;

        public SaveLocator(IEnumerable<ISaveLocator> locators)
        {
            _locators = locators?.ToList() ?? new List<ISaveLocator>();
        }

        public IReadOnlyList<ISaveLocator> Locators => _locators;

        public static SaveLocator CreateDefault()
        {
            return new SaveLocator(new ISaveLocator[]
            {
                new SteamUserDataLocator(),
                new UbisoftSavegamesLocator(),
                new GogGalaxyLocator(),
                new EpicLocator(),
                new CommonSaveRootLocator(),
                new InstallDirectoryLocator(),
            });
        }

        public LocateResult Locate(GameDescriptor game, SaveEnvironment environment)
        {
            var result = new LocateResult();

            if (game == null || environment == null)
            {
                return result;
            }

            var merged = new Dictionary<string, SaveCandidate>(StringComparer.OrdinalIgnoreCase);

            foreach (var locator in _locators)
            {
                IEnumerable<SaveCandidate> found;
                try
                {
                    found = locator.Locate(game, environment);
                }
                catch
                {
                    continue;
                }

                try
                {
                    foreach (var candidate in found)
                    {
                        if (candidate == null
                            || string.IsNullOrWhiteSpace(candidate.Path)
                            || !candidate.Exists)
                        {
                            continue;
                        }

                        var key = NormalizePath(candidate.Path);
                        if (merged.TryGetValue(key, out var existing))
                        {
                            if (candidate.Confidence > existing.Confidence)
                            {
                                merged[key] = candidate;
                            }
                            continue;
                        }

                        merged[key] = candidate;
                    }
                }
                catch
                {
                    // A single locator failing must not break the chain.
                }
            }

            result.Candidates = merged.Values
                .OrderByDescending(x => x.Confidence)
                .ThenBy(x => x.Source)
                .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var best = result.Best;
            result.NeedsConfirmation = best != null && best.Confidence < AutoConfirmThreshold;

            return result;
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path).TrimEnd('\\', '/');
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}
