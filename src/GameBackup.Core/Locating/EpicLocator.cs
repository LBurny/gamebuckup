using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameBackup.Core.Models;
using GameBackup.Core.Scanning;

namespace GameBackup.Core.Locating
{
    /// <summary>
    /// Epic has no single save convention, but Unreal-based titles use
    /// <c>%LOCALAPPDATA%\%AppName%\Saved\SaveGames</c>. The AppName is the
    /// install folder name in most cases, so we try both it and the store id.
    /// </summary>
    public sealed class EpicLocator : ISaveLocator
    {
        public string Id => "epic";

        public IEnumerable<SaveCandidate> Locate(GameDescriptor game, SaveEnvironment environment)
        {
            if (!CandidateFactory.MatchesStore(game, "epic") && !string.IsNullOrEmpty(game.Source))
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(environment.LocalAppData))
            {
                yield break;
            }

            var ids = new List<string>();
            Add(ids, NameMatching.SafeLeaf(game.InstallDirectory));
            Add(ids, game.StoreGameId);
            Add(ids, game.Name);

            foreach (var id in ids)
            {
                var saved = Path.Combine(environment.LocalAppData, id, "Saved");
                var saveGames = Path.Combine(saved, "SaveGames");
                if (CandidateFactory.IsDirectory(saveGames))
                {
                    yield return CandidateFactory.FromPath(
                        saveGames,
                        CandidateKind.Directory,
                        0.88,
                        CandidateSource.Epic,
                        "Epic / Unreal 存档目录（" + id + "\\Saved\\SaveGames）");
                }
            }
        }

        private static void Add(List<string> ids, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var trimmed = value.Trim();
            if (trimmed.Length < 2)
            {
                return;
            }

            if (ids.Any(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            ids.Add(trimmed);
        }
    }
}
