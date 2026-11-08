using System.Threading;
using System.Threading.Tasks;
using GameBackup.Core.Llm;
using GameBackup.Plugin.Settings;

namespace GameBackup.Plugin
{
    /// <summary>
    /// Services the page needs from the Playnite hosting layer.
    /// Keeps the page and view models free of Playnite.SDK.
    /// </summary>
    public interface IGameBackupHost
    {
        GameBackupSettings Settings { get; }

        string PluginDataPath { get; }

        void SaveSettings(GameBackupSettings settings, bool notify = true);

        void Notify(string text, bool isError = false);

        /// <summary>Backup-specific notification that honours the user's notification mode.</summary>
        void NotifyBackup(string text, bool isError = false);

        /// <summary>
        /// True when the game's store syncs saves to the cloud and the user asked
        /// GameBackup not to create a second, local copy for that platform.
        /// </summary>
        bool IsCloudBackupSkipped(string sourceName);

        string SelectFolder(string initialDirectory);

        Task<LlmTestResult> TestLlmAsync(LlmOptions options, CancellationToken cancellationToken);
    }
}
