using System.Collections.Generic;
using System.IO;
using GameBackup.Core.Models;
using GameBackup.Core.Scanning;

namespace GameBackup.Core.Locating
{
    /// <summary>
    /// Ubisoft Connect keeps saves inside the launcher install folder:
    /// <c>savegames/&lt;account&gt;/&lt;uplayId&gt;</c>. Playnite exposes the
    /// Uplay id as the store id, so no guessing is required.
    /// </summary>
    public sealed class UbisoftSavegamesLocator : ISaveLocator
    {
        public string Id => "ubisoft-savegames";

        public IEnumerable<SaveCandidate> Locate(GameDescriptor game, SaveEnvironment environment)
        {
            var gameId = (game.StoreGameId ?? string.Empty).Trim();
            if (!CandidateFactory.IsNumeric(gameId))
            {
                yield break;
            }

            if (!CandidateFactory.MatchesStore(game, "ubisoft", "uplay", "connect")
                && !string.IsNullOrEmpty(game.Source))
            {
                yield break;
            }

            foreach (var root in environment.UbisoftSavegameRoots)
            {
                if (!CandidateFactory.IsDirectory(root))
                {
                    continue;
                }

                var direct = Path.Combine(root, gameId);
                if (CandidateFactory.IsDirectory(direct))
                {
                    yield return CandidateFactory.FromPath(
                        direct,
                        CandidateKind.Directory,
                        0.94,
                        CandidateSource.Ubisoft,
                        "育碧 Connect 存档目录（游戏 ID " + gameId + "）");
                }

                foreach (var account in CandidateFactory.Directories(root, 16))
                {
                    var accountGame = Path.Combine(account, gameId);
                    if (!CandidateFactory.IsDirectory(accountGame))
                    {
                        continue;
                    }

                    yield return CandidateFactory.FromPath(
                        accountGame,
                        CandidateKind.Directory,
                        0.96,
                        CandidateSource.Ubisoft,
                        "育碧 Connect 存档目录（账号 "
                        + Path.GetFileName(account) + "，游戏 ID " + gameId + "）");
                }
            }
        }
    }
}
