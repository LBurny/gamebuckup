using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using GameBackup.Core.Backup;
using GameBackup.Plugin.UI.Infrastructure;

namespace GameBackup.Plugin.UI.ViewModels
{
    /// <summary>One version listed in the backup files window.</summary>
    public sealed class BackupFileViewModel
    {
        public BackupFileViewModel(BackupSummary summary)
        {
            Directory = summary.Directory;
            var local = summary.TimestampUtc.ToLocalTime();
            TimeText = summary.TimestampUtc == DateTime.MinValue
                ? "早期备份"
                : local.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
            FileCountText = summary.FileCount + " 个文件";
            SizeText = GameRowViewModel.FormatSize(summary.TotalBytes);
        }

        public string Directory { get; }

        public string TimeText { get; }

        public string FileCountText { get; }

        public string SizeText { get; }
    }

    /// <summary>
    /// Backing model for the dialog the game menu opens. Mirrors the locate
    /// window: a short header, one card per result and a footer of actions.
    /// </summary>
    public sealed class BackupFilesViewModel : Infrastructure.ObservableObject
    {
        private readonly Action _close;

        public BackupFilesViewModel(
            string gameName,
            string savePath,
            string backupRoot,
            IEnumerable<BackupSummary> versions,
            Action<string> openFolder,
            Action close)
        {
            GameName = gameName ?? string.Empty;
            SavePath = string.IsNullOrWhiteSpace(savePath) ? "尚未定位存档位置" : savePath;
            BackupRoot = string.IsNullOrWhiteSpace(backupRoot) ? "尚未设置备份目录" : backupRoot;
            _close = close ?? delegate { };

            foreach (var version in versions ?? new List<BackupSummary>())
            {
                Versions.Add(new BackupFileViewModel(version));
            }

            OpenVersionCommand = new RelayCommand(parameter =>
            {
                var item = parameter as BackupFileViewModel;
                if (item != null && openFolder != null)
                {
                    openFolder(item.Directory);
                }
            });

            OpenRootCommand = new RelayCommand(_ =>
            {
                if (openFolder != null)
                {
                    openFolder(backupRoot);
                }
            });

            CloseCommand = new RelayCommand(_ => _close());
        }

        public string GameName { get; }

        public string SavePath { get; }

        public string BackupRoot { get; }

        public ObservableCollection<BackupFileViewModel> Versions { get; } =
            new ObservableCollection<BackupFileViewModel>();

        public ICommand OpenVersionCommand { get; }

        public ICommand OpenRootCommand { get; }

        public ICommand CloseCommand { get; }

        public bool HasVersions => Versions.Count > 0;

        public bool ShowEmptyHint => Versions.Count == 0;
    }
}
