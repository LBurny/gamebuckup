using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using GameBackup.Core.Backup;
using GameBackup.Core.Models;
using GameBackup.Plugin.Adapters;
using GameBackup.Plugin.UI.Infrastructure;

namespace GameBackup.Plugin.UI.ViewModels
{
    public sealed class FilterOption : Infrastructure.ObservableObject
    {
        private int _count;

        public FilterOption(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public string Key { get; }

        public string Label { get; }

        public int Count
        {
            get => _count;
            set => Set(ref _count, value);
        }
    }

    public sealed class GameBackupViewModel : Infrastructure.ObservableObject
    {
        private readonly Func<IReadOnlyList<PlayniteGameEntry>> _loader;
        private readonly Action<string, bool> _notify;
        private readonly Action _openSettings;
        private readonly Action<GameRowViewModel> _startLocate;
        private readonly Action<GameRowViewModel> _startBackup;
        private readonly Action<GameRowViewModel> _startRestore;
        private readonly Action<GameRowViewModel> _confirmCandidate;

        private readonly List<GameRowViewModel> _all = new List<GameRowViewModel>();
        private string _searchText = string.Empty;
        private FilterOption _selectedFilter;
        private FilterOption _selectedSource;
        private GameRowViewModel _selectedGame;
        private bool _isScanning;
        private string _scanStatus = string.Empty;
        private bool _applyingFilter;

        public GameBackupViewModel(
            Func<IReadOnlyList<PlayniteGameEntry>> loader,
            Action<string, bool> notify,
            Action openSettings,
            Action<GameRowViewModel> startLocate,
            Action<GameRowViewModel> startBackup,
            Action<GameRowViewModel> startRestore,
            Action<GameRowViewModel> confirmCandidate)
        {
            _loader = loader;
            _notify = notify;
            _openSettings = openSettings;
            _startLocate = startLocate;
            _startBackup = startBackup;
            _startRestore = startRestore;
            _confirmCandidate = confirmCandidate;

            Filters = new ObservableCollection<FilterOption>
            {
                new FilterOption("all", "全部"),
                new FilterOption("located", "已定位"),
                new FilterOption("pending", "待确认"),
                new FilterOption("unknown", "未定位"),
                new FilterOption("backedup", "有备份"),
            };
            _selectedFilter = Filters[0];
            _selectedSource = null;

            RefreshCommand = new RelayCommand(Reload);
            OpenSettingsCommand = new RelayCommand(_openSettings);
            LocateCommand = new RelayCommand(
                _ =>
                {
                    var game = SelectedGame;
                    if (game != null)
                    {
                        _startLocate(game);
                    }
                });
            BackupCommand = new RelayCommand(
                _ =>
                {
                    var game = SelectedGame;
                    if (game != null)
                    {
                        _startBackup(game);
                    }
                });
            RestoreCommand = new RelayCommand(
                _ =>
                {
                    var game = SelectedGame;
                    if (game != null)
                    {
                        _startRestore(game);
                    }
                });
            OpenFolderCommand = new RelayCommand(_ => OpenSelectedFolder());
            ConfirmCommand = new RelayCommand(
                _ =>
                {
                    var game = SelectedGame;
                    if (game != null && game.IsCandidate)
                    {
                        _confirmCandidate(game);
                    }
                });

            Reload();
        }

        /// <summary>Raised after the game list was rebuilt, so the host can re-scan.</summary>
        public event EventHandler Reloaded;

        public ObservableCollection<GameRowViewModel> Games { get; } = new ObservableCollection<GameRowViewModel>();

        public IEnumerable<GameRowViewModel> AllGames => _all;

        public ObservableCollection<FilterOption> Filters { get; }

        public ObservableCollection<FilterOption> Sources { get; } = new ObservableCollection<FilterOption>();

        public ICommand RefreshCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand LocateCommand { get; }

        public ICommand BackupCommand { get; }

        public ICommand RestoreCommand { get; }

        public ICommand OpenFolderCommand { get; }

        public ICommand ConfirmCommand { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value))
                {
                    ApplyFilter();
                }
            }
        }

        public FilterOption SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (Set(ref _selectedFilter, value))
                {
                    ApplyFilter();
                }
            }
        }

        public FilterOption SelectedSource
        {
            get => _selectedSource;
            set
            {
                if (Set(ref _selectedSource, value))
                {
                    ApplyFilter();
                }
            }
        }

        public GameRowViewModel SelectedGame
        {
            get => _selectedGame;
            set
            {
                if (Set(ref _selectedGame, value))
                {
                    RaisePropertyChanged(nameof(HasSelection));
                    RaisePropertyChanged(nameof(SelectedGameName));
                }
            }
        }

        public bool HasSelection => SelectedGame != null;

        public string SelectedGameName => SelectedGame?.Name ?? string.Empty;

        public string SummaryText
        {
            get
            {
                var confirmed = _all.Count(x => x.Status == SaveStatus.Confirmed || x.Status == SaveStatus.Stale);
                var pending = _all.Count(x => x.Status == SaveStatus.Candidate);
                return $"{_all.Count} 款游戏 · {confirmed} 已定位 · {pending} 待确认";
            }
        }

        public bool IsScanning
        {
            get => _isScanning;
            set => Set(ref _isScanning, value);
        }

        /// <summary>Progress line for the automatic platform scan.</summary>
        public string ScanStatus
        {
            get => _scanStatus;
            set => Set(ref _scanStatus, value);
        }

        public void Reload()
        {
            var selectedKey = SelectedGame?.Descriptor.Key;
            var previous = _all
                .GroupBy(x => x.Descriptor.Key)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

            _all.Clear();
            foreach (var entry in _loader())
            {
                var row = new GameRowViewModel(entry.Descriptor, entry.CoverPath);
                if (previous.TryGetValue(entry.Descriptor.Key, out var old))
                {
                    row.SavePath = old.SavePath;
                    row.Status = old.Status;
                    row.LocationSourceText = old.LocationSourceText;
                    row.LastBackupText = old.LastBackupText;
                    row.LastBackupUtc = old.LastBackupUtc;
                    row.BackupSizeText = old.BackupSizeText;
                    row.DetectedSource = old.DetectedSource;
                }
                _all.Add(row);
            }

            UpdateFilterCounts();
            ApplyFilter();
            RaisePropertyChanged(nameof(SummaryText));

            SelectedGame = Games.FirstOrDefault(
                x => string.Equals(x.Descriptor.Key, selectedKey, StringComparison.OrdinalIgnoreCase))
                ?? Games.FirstOrDefault();

            Reloaded?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateFilterCounts()
        {
            Filters[0].Count = _all.Count;
            Filters[1].Count = _all.Count(x => x.Status == SaveStatus.Confirmed || x.Status == SaveStatus.Stale);
            Filters[2].Count = _all.Count(x => x.Status == SaveStatus.Candidate);
            Filters[3].Count = _all.Count(x => x.Status == SaveStatus.Unknown);
            Filters[4].Count = _all.Count(x => x.LastBackupText != "—");

            UpdateSourceFilters();
        }

        /// <summary>
        /// Rebuilds the store filter from the loaded games so only sources that
        /// actually exist are offered. No "all" entry is added: an empty
        /// selection already means every source, which keeps the row free of a
        /// chip that would duplicate the status filter's "all".
        /// </summary>
        private void UpdateSourceFilters()
        {
            var previousKey = _selectedSource?.Key;

            var groups = _all
                .GroupBy(x => x.Source, StringComparer.CurrentCultureIgnoreCase)
                .Select(g => new { Key = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            Sources.Clear();
            foreach (var group in groups)
            {
                Sources.Add(new FilterOption(group.Key, group.Key) { Count = group.Count });
            }

            _selectedSource = string.IsNullOrEmpty(previousKey)
                ? null
                : Sources.FirstOrDefault(
                    x => string.Equals(x.Key, previousKey, StringComparison.CurrentCultureIgnoreCase));
            RaisePropertyChanged(nameof(SelectedSource));
        }

        /// <summary>
        /// Recomputes counts and the visible list after a game's status changed
        /// outside the list itself (for example after AI confirmation).
        /// </summary>
        public void RefreshCounters()
        {
            UpdateFilterCounts();
            ApplyFilter();
            RaisePropertyChanged(nameof(SummaryText));
        }

        /// <summary>
        /// Refreshes only the "has backup" counter after the page reads versions
        /// from disk. A full <see cref="RefreshCounters"/> would rebuild the list
        /// and re-trigger the selection that started the lookup.
        /// </summary>
        public void UpdateBackupCount()
        {
            Filters[4].Count = _all.Count(x => x.LastBackupText != "—");
        }

        /// <summary>
        /// Refreshes the 「有备份」 counter and, when that view is the one on
        /// screen, re-sorts it so the newest backup moves to the top. Other views
        /// are left untouched, so a finished backup never reshuffles the list.
        /// </summary>
        public void RefreshBackupOrder()
        {
            UpdateBackupCount();

            if (SelectedFilter != null && SelectedFilter.Key == "backedup")
            {
                ApplyFilter();
            }
        }

        /// <summary>
        /// Marks every game that already has a backup folder, so the "有备份"
        /// chip and counter are accurate on a fresh session instead of only for
        /// the row the user happens to open.
        /// </summary>
        public void ApplyBackupPresence(IReadOnlyDictionary<string, DateTime> newestByFolder)
        {
            if (newestByFolder == null)
            {
                return;
            }

            foreach (var row in _all)
            {
                DateTime stamp;
                if (!newestByFolder.TryGetValue(LudusaviMapping.EscapeFolderName(row.Name), out stamp))
                {
                    continue;
                }

                row.LastBackupText = stamp == DateTime.MinValue
                    ? "已有备份"
                    : stamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                row.LastBackupUtc = stamp == DateTime.MinValue ? (DateTime?)null : stamp;
            }

            RefreshBackupOrder();
        }

        private void ApplyFilter()
        {
            // Rebuilding the list re-enters through the selection binding, so a
            // nested call (a row re-selecting itself and asking for a re-sort)
            // must be dropped instead of looping.
            if (_applyingFilter)
            {
                return;
            }

            _applyingFilter = true;
            try
            {
                ApplyFilterCore();
            }
            finally
            {
                _applyingFilter = false;
            }
        }

        private void ApplyFilterCore()
        {
            IEnumerable<GameRowViewModel> query = _all;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var needle = SearchText.Trim();
                query = query.Where(
                    x => x.Name.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0
                         || x.Source.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0);
            }

            switch (SelectedFilter?.Key)
            {
                case "located":
                    query = query.Where(x => x.Status == SaveStatus.Confirmed || x.Status == SaveStatus.Stale);
                    break;
                case "pending":
                    query = query.Where(x => x.Status == SaveStatus.Candidate);
                    break;
                case "unknown":
                    query = query.Where(x => x.Status == SaveStatus.Unknown);
                    break;
                case "backedup":
                    query = query.Where(x => x.LastBackupText != "—");
                    break;
            }

            var sourceKey = SelectedSource?.Key;
            if (!string.IsNullOrEmpty(sourceKey) && sourceKey != "all")
            {
                query = query.Where(
                    x => string.Equals(x.Source, sourceKey, StringComparison.CurrentCultureIgnoreCase));
            }

            // 「有备份」 is about age, not about location: newest backup first so the
            // games the user just backed up stay at the top of the list.
            List<GameRowViewModel> ordered;
            if (SelectedFilter?.Key == "backedup")
            {
                ordered = query
                    .OrderByDescending(x => x.LastBackupUtc ?? DateTime.MinValue)
                    .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            else
            {
                ordered = query
                    .OrderByDescending(x => x.Status == SaveStatus.Confirmed)
                    .ThenByDescending(x => x.Status == SaveStatus.Candidate)
                    .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }

            // Clearing the collection makes the list box push null back into
            // SelectedGame, so the selection has to be remembered first. Without
            // this, every counter refresh jumped the user back to the first game.
            var keep = SelectedGame;

            Games.Clear();
            foreach (var row in ordered)
            {
                Games.Add(row);
            }

            if (keep != null && Games.Contains(keep))
            {
                SelectedGame = keep;
                return;
            }

            SelectedGame = Games.FirstOrDefault();
        }

        private void OpenSelectedFolder()
        {
            var game = SelectedGame;
            if (game == null)
            {
                return;
            }

            var path = game.SavePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = game.InstallDirectory;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                _notify("这款游戏没有可打开的目录。", true);
                return;
            }

            try
            {
                var target = File.Exists(path) ? Path.GetDirectoryName(path) : path;
                if (!string.IsNullOrWhiteSpace(target))
                {
                    Process.Start("explorer.exe", "\"" + target + "\"");
                }
            }
            catch (Exception ex)
            {
                _notify("无法打开目录：" + ex.Message, true);
            }
        }
    }
}
