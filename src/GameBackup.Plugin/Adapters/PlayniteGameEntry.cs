using GameBackup.Core.Models;

namespace GameBackup.Plugin.Adapters
{
    /// <summary>
    /// A Playnite game plus the resolved local cover path, before it is turned
    /// into a view model.
    /// </summary>
    public sealed class PlayniteGameEntry
    {
        public GameDescriptor Descriptor { get; set; }

        public string CoverPath { get; set; }
    }
}
