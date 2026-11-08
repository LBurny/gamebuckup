using System;
using System.Collections.Generic;
using System.IO;
using GameBackup.Core.Models;
using GameBackup.Core.Scanning;

namespace GameBackup.Core.Locating
{
    /// <summary>
    /// Probes the usual per-user save roots by folder name. Exact matches inside
    /// the canonical roots are trusted enough to auto-confirm; looser matches are
    /// surfaced as candidates that need a click.
    /// </summary>
    public sealed class CommonSaveRootLocator : ISaveLocator
    {
        public string Id => "common-roots";

        public IEnumerable<SaveCandidate> Locate(GameDescriptor game, SaveEnvironment environment)
        {
            var aliases = NameMatching.Aliases(game);
            if (aliases.Count == 0)
            {
                yield break;
            }

            foreach (var candidate in ProbeRoot(
                         Path.Combine(environment.Documents, "My Games"),
                         aliases,
                         0.86,
                         "常见存档目录（Documents\\My Games）"))
            {
                yield return candidate;
            }

            foreach (var candidate in ProbeRoot(
                         environment.SavedGames,
                         aliases,
                         0.86,
                         "常见存档目录（Saved Games）"))
            {
                yield return candidate;
            }

            foreach (var candidate in ProbeRoot(
                         environment.Documents,
                         aliases,
                         0.86,
                         "常见存档目录（Documents）"))
            {
                yield return candidate;
            }

            foreach (var candidate in ProbeRoot(
                         environment.LocalAppData,
                         aliases,
                         0.84,
                         "常见存档目录（AppData\\Local）"))
            {
                yield return candidate;
            }

            foreach (var candidate in ProbeRoot(
                         environment.RoamingAppData,
                         aliases,
                         0.84,
                         "常见存档目录（AppData\\Roaming）"))
            {
                yield return candidate;
            }

            foreach (var candidate in ProbeLocalLow(environment.LocalLowAppData, aliases))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<SaveCandidate> ProbeRoot(
            string root,
            IReadOnlyList<string> aliases,
            double exactConfidence,
            string evidence)
        {
            if (string.IsNullOrWhiteSpace(root) || !CandidateFactory.IsDirectory(root))
            {
                yield break;
            }

            foreach (var alias in aliases)
            {
                var direct = Path.Combine(root, alias);
                if (CandidateFactory.IsDirectory(direct))
                {
                    yield return CandidateFactory.FromPath(
                        PreferSaveChild(direct),
                        CandidateKind.Directory,
                        exactConfidence,
                        CandidateSource.CommonRoot,
                        evidence);
                    yield break;
                }
            }

            SaveCandidate loose = null;
            foreach (var child in CandidateFactory.Directories(root, 300))
            {
                var score = NameMatching.Score(NameMatching.SafeLeaf(child), aliases);
                if (score >= 0.9)
                {
                    yield return CandidateFactory.FromPath(
                        PreferSaveChild(child),
                        CandidateKind.Directory,
                        exactConfidence,
                        CandidateSource.CommonRoot,
                        evidence + "，名称匹配");
                    yield break;
                }

                if (score >= 0.8 && loose == null)
                {
                    loose = CandidateFactory.FromPath(
                        child,
                        CandidateKind.Directory,
                        0.75,
                        CandidateSource.CommonRoot,
                        evidence + "，名称近似（请确认）");
                }
            }

            if (loose != null)
            {
                yield return loose;
            }
        }

        /// <summary>
        /// LocalLow is normally <c>&lt;company&gt;\&lt;product&gt;</c> (Unity), so
        /// both levels are checked. The company folder itself is not a save dir.
        /// </summary>
        private static IEnumerable<SaveCandidate> ProbeLocalLow(
            string root,
            IReadOnlyList<string> aliases)
        {
            if (string.IsNullOrWhiteSpace(root) || !CandidateFactory.IsDirectory(root))
            {
                yield break;
            }

            foreach (var alias in aliases)
            {
                var direct = Path.Combine(root, alias);
                if (CandidateFactory.IsDirectory(direct))
                {
                    yield return CandidateFactory.FromPath(
                        PreferSaveChild(direct),
                        CandidateKind.Directory,
                        0.84,
                        CandidateSource.CommonRoot,
                        "常见存档目录（AppData\\LocalLow）");
                    yield break;
                }
            }

            SaveCandidate loose = null;
            foreach (var company in CandidateFactory.Directories(root, 200))
            {
                var companyName = NameMatching.SafeLeaf(company);

                foreach (var alias in aliases)
                {
                    var direct = Path.Combine(company, alias);
                    if (CandidateFactory.IsDirectory(direct))
                    {
                        yield return CandidateFactory.FromPath(
                            PreferSaveChild(direct),
                            CandidateKind.Directory,
                            0.88,
                            CandidateSource.CommonRoot,
                            "常见存档目录（AppData\\LocalLow\\" + companyName + "）");
                        yield break;
                    }
                }

                foreach (var product in CandidateFactory.Directories(company, 40))
                {
                    var score = NameMatching.Score(NameMatching.SafeLeaf(product), aliases);
                    if (score >= 0.9)
                    {
                        yield return CandidateFactory.FromPath(
                            PreferSaveChild(product),
                            CandidateKind.Directory,
                            0.86,
                            CandidateSource.CommonRoot,
                            "常见存档目录（AppData\\LocalLow\\" + companyName + "，名称匹配）");
                        yield break;
                    }

                    if (score >= 0.8 && loose == null)
                    {
                        loose = CandidateFactory.FromPath(
                            product,
                            CandidateKind.Directory,
                            0.75,
                            CandidateSource.CommonRoot,
                            "常见存档目录（AppData\\LocalLow\\" + companyName + "，名称近似，请确认）");
                    }
                }
            }

            if (loose != null)
            {
                yield return loose;
            }
        }

        /// <summary>
        /// Narrows a game folder to its save subfolder when one is obvious, so a
        /// backup of "Documents\My Games\Game\Saves" does not also pull in configs
        /// and logs sitting next to it.
        /// </summary>
        private static string PreferSaveChild(string directory)
        {
            if (NameMatching.IsSaveLikeName(NameMatching.SafeLeaf(directory)))
            {
                return directory;
            }

            foreach (var child in CandidateFactory.Directories(directory, 24))
            {
                if (NameMatching.IsSaveLikeName(NameMatching.SafeLeaf(child))
                    && CandidateFactory.HasFiles(child, 16))
                {
                    return child;
                }
            }

            return directory;
        }
    }
}
