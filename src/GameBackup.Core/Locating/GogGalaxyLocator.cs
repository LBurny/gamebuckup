using System.Collections.Generic;
using System.IO;
using GameBackup.Core.Models;
using GameBackup.Core.Scanning;

namespace GameBackup.Core.Locating
{
    /// <summary>
    /// GOG Galaxy mirrors cloud saves into
    /// <c>%LOCALAPPDATA%\GOG.com\Galaxy\Applications\&lt;productId&gt;\Storage</c>.
    /// </summary>
    public sealed class GogGalaxyLocator : ISaveLocator
    {
        public string Id => "gog-galaxy";

        public IEnumerable<SaveCandidate> Locate(GameDescriptor game, SaveEnvironment environment)
        {
            var productId = (game.StoreGameId ?? string.Empty).Trim();
            if (!CandidateFactory.IsNumeric(productId))
            {
                yield break;
            }

            if (!CandidateFactory.MatchesStore(game, "gog") && !string.IsNullOrEmpty(game.Source))
            {
                yield break;
            }

            foreach (var root in environment.GogApplicationRoots)
            {
                if (!CandidateFactory.IsDirectory(root))
                {
                    continue;
                }

                var application = Path.Combine(root, productId);
                if (!CandidateFactory.IsDirectory(application))
                {
                    continue;
                }

                var storage = Path.Combine(application, "Storage");
                if (CandidateFactory.IsDirectory(storage))
                {
                    yield return CandidateFactory.FromPath(
                        storage,
                        CandidateKind.Directory,
                        0.92,
                        CandidateSource.Gog,
                        "GOG Galaxy 云存档目录（产品 ID " + productId + "）");
                }
                else
                {
                    yield return CandidateFactory.FromPath(
                        application,
                        CandidateKind.Directory,
                        0.8,
                        CandidateSource.Gog,
                        "GOG Galaxy 应用目录（产品 ID " + productId + "）");
                }
            }
        }
    }
}
