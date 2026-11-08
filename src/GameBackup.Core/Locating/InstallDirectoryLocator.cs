using System.Collections.Generic;
using System.IO;
using GameBackup.Core.Models;
using GameBackup.Core.Scanning;

namespace GameBackup.Core.Locating
{
    /// <summary>
    /// Last deterministic resort: look inside the install folder for a
    /// save-named directory or an Unreal <c>Saved\SaveGames</c> tree. This is
    /// what rescues manually installed games that no store knows about.
    /// </summary>
    public sealed class InstallDirectoryLocator : ISaveLocator
    {
        public string Id => "install-dir";

        public IEnumerable<SaveCandidate> Locate(GameDescriptor game, SaveEnvironment environment)
        {
            var install = (game.InstallDirectory ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(install) || !CandidateFactory.IsDirectory(install))
            {
                yield break;
            }

            var direct = Path.Combine(install, "Saved", "SaveGames");
            if (CandidateFactory.IsDirectory(direct))
            {
                yield return CandidateFactory.FromPath(
                    direct,
                    CandidateKind.Directory,
                    0.88,
                    CandidateSource.InstallDir,
                    "安装目录内的 Unreal 存档目录（Saved\\SaveGames）");
            }

            var emitted = 0;
            foreach (var child in CandidateFactory.Directories(install, 80))
            {
                var name = NameMatching.SafeLeaf(child);

                if (NameMatching.IsSaveLikeName(name))
                {
                    yield return CandidateFactory.FromPath(
                        child,
                        CandidateKind.Directory,
                        0.74,
                        CandidateSource.InstallDir,
                        "安装目录内的存档文件夹（" + name + "）");
                    emitted++;
                }

                var unreal = Path.Combine(child, "Saved", "SaveGames");
                if (CandidateFactory.IsDirectory(unreal))
                {
                    yield return CandidateFactory.FromPath(
                        unreal,
                        CandidateKind.Directory,
                        0.86,
                        CandidateSource.InstallDir,
                        "安装目录内的 Unreal 存档目录（" + name + "\\Saved\\SaveGames）");
                    emitted++;
                }

                if (emitted >= 4)
                {
                    yield break;
                }
            }
        }
    }
}
