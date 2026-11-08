namespace GameBackup.Core.Models
{
    /// <summary>
    /// A host-agnostic view of a game. Playnite types never cross into Core.
    /// </summary>
    public sealed class GameDescriptor
    {
        /// <summary>Stable identity, for example "playnite:&lt;guid&gt;".</summary>
        public string Key { get; set; }

        public string Name { get; set; }

        public string InstallDirectory { get; set; }

        /// <summary>Store or library name, for example "Steam" or "GOG".</summary>
        public string Source { get; set; }

        /// <summary>Store-specific id, for example a Steam AppID. Empty when unknown.</summary>
        public string StoreGameId { get; set; }

        public string Platform { get; set; }

        public bool IsInstalled { get; set; }

        /// <summary>
        /// Changes when the install location or store identity changes, so cached
        /// save-location results can be invalidated.
        /// </summary>
        public string Fingerprint { get; set; }
    }
}
