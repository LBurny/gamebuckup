using System;
using System.Collections.Generic;
using System.IO;

namespace GameBackup.Plugin.Settings
{
    public enum BackupNotificationMode
    {
        /// <summary>Show a notification after every backup.</summary>
        Always = 0,

        /// <summary>Only speak up when a backup fails.</summary>
        FailuresOnly = 1,
    }

    public sealed class GameBackupSettings
    {
        public string BackupRoot { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GameBackup");

        /// <summary>Last width of the game list pane, remembered across sessions.</summary>
        public double ListPaneWidth { get; set; } = 380;

        public bool AutoBackupOnExit { get; set; }

        /// <summary>Ask before backing up when a game exits (four-choice prompt).</summary>
        public bool AskOnGameExit { get; set; }

        public BackupNotificationMode BackupNotification { get; set; } = BackupNotificationMode.Always;

        /// <summary>Per-game answers from the exit prompt (true = always back up).</summary>
        public Dictionary<string, bool> ExitBackupChoices { get; set; } = new Dictionary<string, bool>();

        public int KeepBackups { get; set; } = 10;

        /// <summary>
        /// Skip games whose store already syncs saves to the cloud. The platform
        /// list below decides which stores count.
        /// </summary>
        public bool SkipCloudSyncedBackups { get; set; }

        /// <summary>Platform keys (see CloudPlatforms) skipped while the option is on.</summary>
        public List<string> SkipCloudPlatforms { get; set; } = new List<string>
        {
            "Epic",
            "GOG",
            "Origin",
            "Steam",
            "Ubisoft",
        };

        public bool EnableLlm { get; set; }

        public string LlmEndpoint { get; set; } = "https://api.deepseek.com/v1";

        public string LlmModel { get; set; } = "deepseek-chat";

        public string LlmApiKeyProtected { get; set; } = string.Empty;
    }
}
