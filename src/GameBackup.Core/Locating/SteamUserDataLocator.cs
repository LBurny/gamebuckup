using System.Collections.Generic;
using System.IO;
using GameBackup.Core.Models;
using GameBackup.Core.Scanning;

namespace GameBackup.Core.Locating
{
    /// <summary>
    /// Steam stores cloud-enabled saves under
    /// <c>&lt;steam&gt;/userdata/&lt;account&gt;/&lt;appId&gt;/remote</c>.
    /// The AppID comes straight from Playnite, so this is the highest-value
    /// deterministic locator.
    /// </summary>
    public sealed class SteamUserDataLocator : ISaveLocator
    {
        public string Id => "steam-userdata";

        public IEnumerable<SaveCandidate> Locate(GameDescriptor game, SaveEnvironment environment)
        {
            var appId = (game.StoreGameId ?? string.Empty).Trim();
            if (!CandidateFactory.IsNumeric(appId))
            {
                yield break;
            }

            if (!CandidateFactory.MatchesStore(game, "steam") && !string.IsNullOrEmpty(game.Source))
            {
                yield break;
            }

            foreach (var root in environment.SteamRoots)
            {
                var userdata = Path.Combine(root, "userdata");
                if (!CandidateFactory.IsDirectory(userdata))
                {
                    continue;
                }

                foreach (var account in CandidateFactory.Directories(userdata, 16))
                {
                    var appDirectory = Path.Combine(account, appId);
                    if (!CandidateFactory.IsDirectory(appDirectory))
                    {
                        continue;
                    }

                    var remote = Path.Combine(appDirectory, "remote");
                    if (CandidateFactory.IsDirectory(remote) && CandidateFactory.HasFiles(remote, 64))
                    {
                        yield return CandidateFactory.FromPath(
                            remote,
                            CandidateKind.Directory,
                            0.95,
                            CandidateSource.Steam,
                            "Steam 云存档目录（AppID " + appId + "）");
                    }
                    else if (HasLocalPayload(appDirectory))
                    {
                        // No cloud saves, but the app folder itself carries data.
                        yield return CandidateFactory.FromPath(
                            appDirectory,
                            CandidateKind.Directory,
                            0.82,
                            CandidateSource.Steam,
                            "Steam 用户数据目录（AppID " + appId + "，本地存档）");
                    }
                }
            }
        }

        /// <summary>
        /// Steam creates an app folder holding only <c>remotecache.vdf</c> for
        /// cloud-enabled games that keep their saves elsewhere. Those folders are
        /// dead ends, so require some other file or a nested folder.
        /// </summary>
        private static bool HasLocalPayload(string appDirectory)
        {
            try
            {
                foreach (var entry in System.IO.Directory.EnumerateFileSystemEntries(appDirectory))
                {
                    if (System.IO.Directory.Exists(entry))
                    {
                        var name = System.IO.Path.GetFileName(entry);
                        if (!string.Equals(name, "remote", System.StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                        continue;
                    }

                    var fileName = System.IO.Path.GetFileName(entry);
                    if (!string.Equals(fileName, "remotecache.vdf", System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Unreadable app folder: treat as no evidence.
            }

            return false;
        }
    }
}
