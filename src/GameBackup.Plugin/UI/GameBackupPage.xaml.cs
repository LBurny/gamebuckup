using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using GameBackup.Core.Backup;
using GameBackup.Core.Llm;
using GameBackup.Core.Locating;
using GameBackup.Core.Models;
using GameBackup.Core.Rules;
using GameBackup.Core.Scanning;
using GameBackup.Plugin.Adapters;
using GameBackup.Plugin.Settings;
using GameBackup.Plugin.UI.ViewModels;
using Playnite.SDK;

namespace GameBackup.Plugin.UI
{
    public partial class GameBackupPage : UserControl
    {
        private readonly IGameBackupHost _host;
        private readonly GameBackupViewModel _viewModel;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly RuleStore _rules;
        private readonly LlmClient _llmClient = new LlmClient();
        private readonly SaveLocator _locator = SaveLocator.CreateDefault();
        private readonly SaveLocationService _locationService;
        private readonly SaveEnvironment _environment;
        private readonly BackupService _backupService = new BackupService();
        private readonly RestoreService _restoreService = new RestoreService();
        private readonly string _homeDirectory;
        private bool _autoScanRunning;
        private bool _backupRunning;

        public GameBackupPage(IPlayniteAPI api, IGameBackupHost host)
        {
            _host = host;
            InitializeComponent();
            ThemeBridge.Ensure(this);
            ApplySavedPaneWidth();

            _rules = new RuleStore(Path.Combine(host.PluginDataPath ?? string.Empty, "rules.json"));
            _homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _environment = WindowsSaveEnvironment.Create();
            _locationService = new SaveLocationService(_locator, _llmClient, _homeDirectory);

            var source = new PlayniteGameSource(api);
            _viewModel = new GameBackupViewModel(
                source.Snapshot,
                (text, isError) => host.Notify(text, isError),
                ShowSettings,
                StartLocate,
                StartBackup,
                StartRestore,
                ConfirmCandidate);
            DataContext = _viewModel;
            ApplySavedRules();
            _viewModel.Reloaded += OnReloaded;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            _settingsViewModel = new SettingsViewModel(host, HideSettings);
            SettingsHost.Content = new SettingsView { DataContext = _settingsViewModel };

            // The first Reload runs inside the view model constructor, before the
            // page can subscribe, so both deferred scans are started here.
            _ = RefreshBackupPresenceAsync();
            _ = AutoLocateAsync();
        }

        private void OnReloaded(object sender, EventArgs e)
        {
            _ = AutoLocateAsync();
            _ = RefreshBackupPresenceAsync();
        }

        /// <summary>
        /// Reads which game folders already hold backups so every row (not just
        /// the selected one) shows a date and the "有备份" filter works right
        /// after the page loads.
        /// </summary>
        private async Task RefreshBackupPresenceAsync()
        {
            var root = _host.Settings.BackupRoot;
            var newest = await Task.Run(() => BackupInventory.GetNewestTimestamps(root));
            _viewModel.ApplyBackupPresence(newest);
        }

        /// <summary>Restores the list width the user last dragged to.</summary>
        private void ApplySavedPaneWidth()
        {
            var saved = _host.Settings.ListPaneWidth;
            if (saved >= ListPaneColumn.MinWidth && saved <= 1600)
            {
                ListPaneColumn.Width = new GridLength(saved, GridUnitType.Pixel);
            }
        }

        private void OnSplitterDragCompleted(object sender, DragCompletedEventArgs e)
        {
            var width = ListPaneColumn.ActualWidth;
            if (width < ListPaneColumn.MinWidth)
            {
                return;
            }

            _host.Settings.ListPaneWidth = Math.Round(width);
            _host.SaveSettings(_host.Settings, false);
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(GameBackupViewModel.SelectedGame), StringComparison.Ordinal))
            {
                _ = LoadBackupSummaryAsync(_viewModel.SelectedGame);
            }
        }

        /// <summary>
        /// Fills the backup overview and history from disk so a restart does not
        /// claim that no backup exists while a previous run is still there.
        /// </summary>
        private async Task LoadBackupSummaryAsync(GameRowViewModel row)
        {
            if (row == null)
            {
                return;
            }

            var root = _host.Settings.BackupRoot;
            var name = row.Name;

            var versions = await Task.Run(() => BackupInventory.GetVersions(root, name));

            row.SetVersions(versions);
            if (versions.Count > 0)
            {
                ApplySummary(row, versions[0]);
            }
        }

        private void ApplySummary(GameRowViewModel row, BackupSummary summary)
        {
            row.LastBackupText = summary.TimestampUtc == DateTime.MinValue
                ? "已有备份"
                : summary.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            row.LastBackupUtc = summary.TimestampUtc == DateTime.MinValue
                ? (DateTime?)null
                : summary.TimestampUtc;
            row.BackupSizeText = GameRowViewModel.FormatSize(summary.TotalBytes);
        }

        private void StartBackup(GameRowViewModel row)
        {
            _ = StartBackupAsync(row);
        }

        private void StartRestore(GameRowViewModel row)
        {
            _ = StartRestoreAsync(row);
        }

        private async Task StartRestoreAsync(GameRowViewModel row)
        {
            if (row == null)
            {
                return;
            }

            if (!row.HasVersions)
            {
                _host.NotifyBackup("「" + row.Name + "」还没有可恢复的备份。", true);
                return;
            }

            var owner = Window.GetWindow(this);
            var confirm = MessageBox.Show(
                owner,
                "要用最近的备份覆盖「" + row.Name + "」的存档吗？",
                "GameBackup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            if (_backupRunning)
            {
                _host.NotifyBackup("已有备份或恢复任务在进行中，请稍候。");
                return;
            }

            _backupRunning = true;

            try
            {
                var root = _host.Settings.BackupRoot;
                var name = row.Name;

                var result = await Task.Run(() => _restoreService.Restore(root, name, null));

                if (!result.Success)
                {
                    _host.NotifyBackup("恢复失败：" + result.Error, true);
                    return;
                }

                _host.NotifyBackup(
                    "已恢复「" + name + "」：" + result.FileCount + " 个文件，"
                    + GameRowViewModel.FormatSize(result.TotalBytes));
            }
            catch (Exception ex)
            {
                _host.NotifyBackup("恢复失败：" + ex.Message, true);
            }
            finally
            {
                _backupRunning = false;
            }
        }

        private async Task StartBackupAsync(GameRowViewModel row)
        {
            if (row == null)
            {
                return;
            }

            if (_host.IsCloudBackupSkipped(row.Source))
            {
                _host.NotifyBackup(
                    "已跳过「" + row.Name + "」：" + row.Source + " 的云存档已覆盖，设置里关闭了该平台的本地备份。");
                return;
            }

            if (!row.HasLocation)
            {
                _host.NotifyBackup("请先定位存档位置，再执行备份。", true);
                return;
            }

            if (_backupRunning)
            {
                _host.NotifyBackup("已有备份任务在进行中，请稍候。");
                return;
            }

            _backupRunning = true;

            try
            {
                var settings = _host.Settings;
                var backupRoot = settings.BackupRoot;
                var keep = settings.KeepBackups;
                var name = row.Name;
                var savePath = row.SavePath;
                var stamp = DateTime.UtcNow;

                var result = await Task.Run(
                    () => _backupService.Backup(backupRoot, name, savePath, stamp, keep));

                if (!result.Success)
                {
                    _host.NotifyBackup("备份失败：" + result.Error, true);
                    return;
                }

                row.LastBackupText = stamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                row.LastBackupUtc = stamp;
                row.BackupSizeText = GameRowViewModel.FormatSize(result.TotalBytes);

                await LoadBackupSummaryAsync(row);

                // A finished backup may change the order; selecting a row must not,
                // or re-selecting itself would rebuild the list forever.
                _viewModel.RefreshBackupOrder();

                _host.NotifyBackup($"已备份「{name}」：{result.FileCount} 个文件，{GameRowViewModel.FormatSize(result.TotalBytes)}");
            }
            catch (Exception ex)
            {
                _host.NotifyBackup("备份失败：" + ex.Message, true);
            }
            finally
            {
                _backupRunning = false;
            }
        }

        /// <summary>
        /// Source chips behave like Playnite's library toggles: clicking the
        /// active chip clears the filter, so no separate "all sources" entry is
        /// needed next to the status filter's own "all".
        /// </summary>
        private void OnSourceFilterClicked(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is ListBox list) || !(e.OriginalSource is DependencyObject source))
            {
                return;
            }

            var item = ItemsControl.ContainerFromElement(list, source) as ListBoxItem;
            if (item == null || !item.IsSelected)
            {
                return;
            }

            list.SelectedItem = null;
            e.Handled = true;
        }

        /// <summary>
        /// Runs the deterministic store locators over every game that has no
        /// known save path yet. This is what makes Steam/Uplay/GOG libraries
        /// show up as located without spending an AI call.
        /// </summary>
        private async Task AutoLocateAsync()
        {
            if (_autoScanRunning)
            {
                return;
            }

            _autoScanRunning = true;

            try
            {
                var rows = _viewModel.AllGames
                    .Where(x => x.Status == SaveStatus.Unknown)
                    .ToList();

                if (rows.Count == 0)
                {
                    return;
                }

                _viewModel.IsScanning = true;
                _viewModel.ScanStatus = "正在按商店 ID 与常见存档目录自动识别…";

                var environment = _environment;
                var locator = _locator;
                var hits = await Task.Run(
                    () => rows.Select(row => new ScanHit
                    {
                        Row = row,
                        Result = locator.Locate(row.Descriptor, environment),
                    }).ToList());

                var confirmed = 0;
                var pending = 0;
                var bySource = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var hit in hits)
                {
                    var best = hit.Result.Best;
                    if (best == null)
                    {
                        continue;
                    }

                    ApplyDetected(hit.Row, best);

                    if (hit.Row.Status == SaveStatus.Confirmed)
                    {
                        confirmed++;
                        var label = SourceLabels.Describe(best.Source);
                        bySource[label] = (bySource.TryGetValue(label, out var count) ? count : 0) + 1;
                    }
                    else
                    {
                        pending++;
                    }
                }

                _viewModel.RefreshCounters();

                var parts = new List<string>();
                if (confirmed > 0)
                {
                    parts.Add($"{confirmed} 款已定位");
                }
                if (pending > 0)
                {
                    parts.Add($"{pending} 款待确认");
                }

                if (parts.Count == 0)
                {
                    _viewModel.ScanStatus = "自动识别没有找到新的存档目录，可以点“查找存档”让 AI 再试一次。";
                }
                else
                {
                    var detail = string.Join(
                        " · ",
                        bySource.OrderByDescending(x => x.Value)
                            .Select(x => x.Key + " " + x.Value));

                    _viewModel.ScanStatus = "自动识别完成：" + string.Join("，", parts)
                                            + (string.IsNullOrEmpty(detail) ? string.Empty : "（" + detail + "）")
                                            + "。";
                }
            }
            catch (Exception ex)
            {
                _host.Notify("自动识别存档目录失败：" + ex.Message, true);
            }
            finally
            {
                _viewModel.IsScanning = false;
                _autoScanRunning = false;
            }
        }

        private static void ApplyDetected(GameRowViewModel row, SaveCandidate best)
        {
            var autoConfirmed = best.Exists && best.Confidence >= SaveLocator.AutoConfirmThreshold;

            row.SavePath = best.Path;
            row.DetectedSource = best.Source;
            row.Status = autoConfirmed ? SaveStatus.Confirmed : SaveStatus.Candidate;
            row.LocationSourceText = SourceLabels.Describe(best.Source)
                                     + (autoConfirmed ? " · 自动识别" : " · 待确认");
        }

        private void ShowSettings()
        {
            _settingsViewModel.Load();
            SettingsOverlay.Visibility = Visibility.Visible;
        }

        private void HideSettings()
        {
            SettingsOverlay.Visibility = Visibility.Collapsed;
            _viewModel.Reload();
        }

        private void StartLocate(GameRowViewModel row)
        {
            if (row == null)
            {
                return;
            }

            LlmOptions llmOptions = null;
            var skipReason = "未启用 AI，仅使用了平台与常见目录识别结果。";

            if (_host.Settings.EnableLlm)
            {
                var apiKey = SecretProtector.Unprotect(_host.Settings.LlmApiKeyProtected);
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    skipReason = "未填写 AI API Key，仅使用了平台与常见目录识别结果。";
                }
                else
                {
                    skipReason = null;
                    llmOptions = new LlmOptions
                    {
                        Endpoint = _host.Settings.LlmEndpoint,
                        Model = _host.Settings.LlmModel,
                        ApiKey = apiKey,
                        TimeoutSeconds = 90,
                    };
                }
            }

            var locate = new LocateViewModel(
                row.Name,
                row.InstallDirectory,
                token => _locationService.LocateAsync(
                    row.Descriptor,
                    _environment,
                    llmOptions,
                    skipReason,
                    token),
                candidate => ApplyCandidate(row, candidate),
                HideLocate);

            LocateHost.Content = new LocateView { DataContext = locate };
            LocateOverlay.Visibility = Visibility.Visible;
            locate.Start();
        }

        private void ConfirmCandidate(GameRowViewModel row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.SavePath))
            {
                return;
            }

            ApplyCandidate(row, new SaveCandidate
            {
                Path = row.SavePath,
                Kind = File.Exists(row.SavePath) ? CandidateKind.File : CandidateKind.Directory,
                Confidence = 1.0,
                Source = row.DetectedSource,
                Evidence = "用户确认",
                Exists = true,
            });
        }

        private void ApplyCandidate(GameRowViewModel row, SaveCandidate candidate)
        {
            if (row == null || candidate == null)
            {
                return;
            }

            row.SavePath = candidate.Path;
            row.DetectedSource = candidate.Source;
            row.Status = SaveStatus.Confirmed;
            row.LocationSourceText = SourceLabels.Describe(candidate.Source) + " · 已确认";

            _rules.Upsert(new GameRule
            {
                Key = row.Descriptor.Key,
                Name = row.Name,
                AppId = row.Descriptor.StoreGameId,
                InstallDir = row.Descriptor.InstallDirectory,
                Saves = new[] { candidate.Path },
                Source = SourceLabels.RuleSource(candidate.Source),
                ConfirmedAt = DateTime.UtcNow.ToString("o"),
            });

            _viewModel.RefreshCounters();
            _host.Notify("已保存存档位置规则：" + row.Name + "（下次会优先使用）");
        }

        private void ApplySavedRules()
        {
            var set = _rules.Load();
            foreach (var row in _viewModel.AllGames)
            {
                var rule = set.Rules.FirstOrDefault(
                    x => string.Equals(x.Key, row.Descriptor.Key, StringComparison.OrdinalIgnoreCase));

                if (rule?.Saves == null || rule.Saves.Length == 0 || string.IsNullOrWhiteSpace(rule.Saves[0]))
                {
                    continue;
                }

                var path = rule.Saves[0];
                var exists = File.Exists(path) || Directory.Exists(path);

                row.SavePath = path;
                row.DetectedSource = CandidateSource.UserRule;
                row.Status = exists ? SaveStatus.Confirmed : SaveStatus.Stale;
                row.LocationSourceText = (exists ? "本地规则 · " : "本地规则失效 · ")
                                         + SourceLabels.DescribeRuleSource(rule.Source);
            }

            _viewModel.RefreshCounters();
        }

        private void HideLocate()
        {
            if (LocateHost.Content is LocateView view && view.DataContext is LocateViewModel vm)
            {
                vm.Stop();
            }

            LocateHost.Content = null;
            LocateOverlay.Visibility = Visibility.Collapsed;
        }

        private sealed class ScanHit
        {
            public GameRowViewModel Row { get; set; }

            public LocateResult Result { get; set; }
        }
    }
}
