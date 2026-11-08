using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using GameBackup.Core.Backup;
using GameBackup.Core.Models;

namespace GameBackup.Plugin.UI.ViewModels
{
    /// <summary>One row in the detail pane's backup history.</summary>
    public sealed class BackupVersionItem
    {
        public BackupVersionItem(BackupSummary summary)
        {
            var local = summary.TimestampUtc.ToLocalTime();
            TimeText = summary.TimestampUtc == DateTime.MinValue
                ? "早期备份"
                : local.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
            FileCountText = summary.FileCount + " 个文件";
            SizeText = GameRowViewModel.FormatSize(summary.TotalBytes);
        }

        public string TimeText { get; }

        public string FileCountText { get; }

        public string SizeText { get; }
    }

    public sealed class GameRowViewModel : Infrastructure.ObservableObject
    {
        private SaveStatus _status = SaveStatus.Unknown;
        private string _savePath = string.Empty;
        private string _lastBackupText = "—";
        private string _backupSizeText = string.Empty;
        private string _locationSourceText = string.Empty;
        private CandidateSource _detectedSource = CandidateSource.UserRule;

        public GameRowViewModel(GameDescriptor descriptor, string coverPath)
        {
            Descriptor = descriptor;
            CoverPath = coverPath ?? string.Empty;
        }

        public GameDescriptor Descriptor { get; }

        /// <summary>
        /// Source that produced <see cref="SavePath"/> when it is only a candidate,
        /// so the confirm action can persist the correct origin.
        /// </summary>
        public CandidateSource DetectedSource
        {
            get => _detectedSource;
            set => Set(ref _detectedSource, value);
        }

        public string Name => Descriptor.Name;

        public string Source => string.IsNullOrEmpty(Descriptor.Source) ? "手动导入" : Descriptor.Source;

        public string InstallDirectory => Descriptor.InstallDirectory;

        public string CoverPath { get; }

        public string Initial =>
            string.IsNullOrEmpty(Name) ? "?" : Name.Substring(0, 1).ToUpperInvariant();

        public SaveStatus Status
        {
            get => _status;
            set
            {
                if (Set(ref _status, value))
                {
                    RaisePropertyChanged(nameof(StatusText));
                    RaisePropertyChanged(nameof(HasLocation));
                    RaisePropertyChanged(nameof(IsCandidate));
                }
            }
        }

        public string StatusText
        {
            get
            {
                switch (Status)
                {
                    case SaveStatus.Confirmed:
                        return "已定位";
                    case SaveStatus.Candidate:
                        return "待确认";
                    case SaveStatus.Stale:
                        return "路径失效";
                    case SaveStatus.Error:
                        return "错误";
                    default:
                        return "未定位";
                }
            }
        }

        public string SavePath
        {
            get => _savePath;
            set
            {
                if (Set(ref _savePath, value))
                {
                    RaisePropertyChanged(nameof(HasLocation));
                }
            }
        }

        public string LocationSourceText
        {
            get => _locationSourceText;
            set => Set(ref _locationSourceText, value);
        }

        public bool HasLocation => !string.IsNullOrWhiteSpace(SavePath);

        /// <summary>True when the path was found automatically but needs a click.</summary>
        public bool IsCandidate => Status == SaveStatus.Candidate;

        public string LastBackupText
        {
            get => _lastBackupText;
            set => Set(ref _lastBackupText, value);
        }

        /// <summary>
        /// Newest backup timestamp in UTC. Null when the game has no backup or the
        /// age of an early backup is unknown, which sorts such rows last.
        /// </summary>
        public DateTime? LastBackupUtc { get; set; }

        public string BackupSizeText
        {
            get => _backupSizeText;
            set => Set(ref _backupSizeText, value);
        }

        public ObservableCollection<BackupVersionItem> Versions { get; } =
            new ObservableCollection<BackupVersionItem>();

        public bool HasVersions => Versions.Count > 0;

        public void SetVersions(IEnumerable<BackupSummary> versions)
        {
            Versions.Clear();
            foreach (var version in versions)
            {
                Versions.Add(new BackupVersionItem(version));
            }

            RaisePropertyChanged(nameof(HasVersions));
        }

        public static string FormatSize(long bytes)
        {
            if (bytes <= 0)
            {
                return string.Empty;
            }

            string[] units = { "B", "KB", "MB", "GB" };
            var value = (double)bytes;
            var unit = 0;

            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return value.ToString(unit == 0 ? "0" : "0.#", CultureInfo.CurrentCulture) + " " + units[unit];
        }
    }
}
