using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GameBackup.Core;
using GameBackup.Core.Backup;
using GameBackup.Core.Llm;
using GameBackup.Core.Locating;
using GameBackup.Core.Models;
using GameBackup.Core.Rules;
using GameBackup.Core.Scanning;
using GameBackup.Plugin.Adapters;
using GameBackup.Plugin.Settings;
using GameBackup.Plugin.UI;
using GameBackup.Plugin.UI.ViewModels;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace GameBackup.Plugin
{
    public sealed class GameBackupPlugin : GenericPlugin, IGameBackupHost
    {
        public static readonly Guid PluginId = Guid.Parse("8f4a2c1e-6b7d-4e3a-9c25-1d8b7a6f3e21");

        private readonly IPlayniteAPI _api;
        private readonly LlmClient _llmClient = new LlmClient();
        private readonly SaveLocator _locator = SaveLocator.CreateDefault();
        private readonly SaveEnvironment _environment = WindowsSaveEnvironment.Create();
        private readonly BackupService _backupService = new BackupService();
        private readonly RestoreService _restoreService = new RestoreService();
        private readonly RuleStore _rules;
        private GameBackupSettings _settings;
        private GameBackupPage _page;

        public GameBackupPlugin(IPlayniteAPI api) : base(api)
        {
            _api = api;
            _settings = LoadPluginSettings<GameBackupSettings>() ?? new GameBackupSettings();
            _rules = new RuleStore(Path.Combine(GetPluginUserDataPath() ?? string.Empty, "rules.json"));
            Properties = new GenericPluginProperties { HasSettings = false };
        }

        public override Guid Id => PluginId;

        public GameBackupSettings Settings => _settings;

        public string PluginDataPath
        {
            get
            {
                var path = GetPluginUserDataPath();
                if (!string.IsNullOrEmpty(path))
                {
                    Directory.CreateDirectory(path);
                }
                return path;
            }
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return new SidebarItem
            {
                Type = SiderbarItemType.View,
                Title = "GameBackup",
                Icon = new TextBlock
                {
                    Text = "\uE74E",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 22,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                Opened = OpenPage,
                Visible = true,
            };
        }

        public void SaveSettings(GameBackupSettings settings, bool notify = true)
        {
            _settings = settings;
            SavePluginSettings(settings);

            if (notify)
            {
                Notify("GameBackup 设置已保存");
            }
        }

        public void Notify(string text, bool isError = false)
        {
            Post(() =>
            {
                try
                {
                    _api.Notifications.Add(
                        new NotificationMessage(
                            "gamebackup-" + Guid.NewGuid().ToString("N"),
                            text,
                            isError ? NotificationType.Error : NotificationType.Info));
                }
                catch
                {
                    // Notifications are best-effort; never break the caller.
                }
            });
        }

        public void NotifyBackup(string text, bool isError = false)
        {
            if (!isError && _settings.BackupNotification == BackupNotificationMode.FailuresOnly)
            {
                return;
            }

            Notify(text, isError);
        }

        public bool IsCloudBackupSkipped(string sourceName)
        {
            return CloudPlatforms.ShouldSkip(
                _settings.SkipCloudSyncedBackups,
                _settings.SkipCloudPlatforms,
                sourceName);
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            yield return new GameMenuItem
            {
                MenuSection = "GameBackup",
                Description = "为所选游戏备份存档",
                Action = a => BackupGames(a.Games),
            };

            yield return new GameMenuItem
            {
                MenuSection = "GameBackup",
                Description = "为所选游戏恢复存档",
                Action = a => RestoreGames(a.Games),
            };

            yield return new GameMenuItem
            {
                MenuSection = "GameBackup",
                Description = "查找所选游戏的备份文件",
                Action = a => RevealBackups(a.Games),
            };
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            var game = args?.Game;
            if (game == null)
            {
                return;
            }

            try
            {
                // A cloud-synced platform is skipped entirely on exit: no prompt,
                // no automatic backup, and no remembered choice to honour.
                if (IsCloudBackupSkipped(game.Source?.Name))
                {
                    return;
                }

                var key = "playnite:" + game.Id;
                var choices = _settings.ExitBackupChoices ??
                              (_settings.ExitBackupChoices = new Dictionary<string, bool>());

                bool remembered;
                if (choices.TryGetValue(key, out remembered))
                {
                    if (remembered)
                    {
                        QueueBackup(game);
                    }

                    return;
                }

                if (!_settings.AskOnGameExit)
                {
                    if (_settings.AutoBackupOnExit)
                    {
                        QueueBackup(game);
                    }

                    return;
                }

                // Staying silent when there is nothing locatable avoids nagging
                // about games whose saves were never found.
                if (string.IsNullOrEmpty(ResolveSavePath(game)))
                {
                    return;
                }

                switch (AskExitBackup(game.Name))
                {
                    case ExitChoice.Always:
                        choices[key] = true;
                        SavePluginSettings(_settings);
                        QueueBackup(game);
                        break;
                    case ExitChoice.Never:
                        choices[key] = false;
                        SavePluginSettings(_settings);
                        break;
                    case ExitChoice.Once:
                        QueueBackup(game);
                        break;
                }
            }
            catch (Exception ex)
            {
                Notify("GameBackup 退出备份失败：" + ex.Message, true);
            }
        }

        private enum ExitChoice
        {
            Always,
            Never,
            Once,
            Skip,
        }

        private ExitChoice AskExitBackup(string gameName)
        {
            var always = new MessageBoxOption("是，总是");
            var never = new MessageBoxOption("否，永不");
            var once = new MessageBoxOption("是", true);
            var skip = new MessageBoxOption("否", false, true);

            var chosen = _api.Dialogs.ShowMessage(
                "要备份「" + gameName + "」的存档数据吗？",
                "GameBackup",
                MessageBoxImage.Question,
                new List<MessageBoxOption> { always, never, once, skip });

            if (chosen == always)
            {
                return ExitChoice.Always;
            }

            if (chosen == never)
            {
                return ExitChoice.Never;
            }

            return chosen == once ? ExitChoice.Once : ExitChoice.Skip;
        }

        private void BackupGames(IEnumerable<Game> games)
        {
            var targets = (games ?? Enumerable.Empty<Game>()).Where(x => x != null).ToList();
            if (targets.Count == 0)
            {
                return;
            }

            Task.Run(() =>
            {
                foreach (var game in targets)
                {
                    BackupGame(game);
                }
            });
        }

        private void RestoreGames(IEnumerable<Game> games)
        {
            var targets = (games ?? Enumerable.Empty<Game>()).Where(x => x != null).ToList();
            if (targets.Count == 0)
            {
                return;
            }

            var confirm = _api.Dialogs.ShowMessage(
                "要用最近的备份覆盖「" + targets[0].Name + "」的存档吗？",
                "GameBackup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            Task.Run(() =>
            {
                foreach (var game in targets)
                {
                    RestoreGame(game);
                }
            });
        }

        private void QueueBackup(Game game)
        {
            Task.Run(() => BackupGame(game));
        }

        /// <summary>
        /// Opens the same dialog pattern as 「重新查找」: a Playnite-themed window
        /// listing every backup version for the first selected game.
        /// </summary>
        private void RevealBackups(IEnumerable<Game> games)
        {
            var game = (games ?? Enumerable.Empty<Game>()).FirstOrDefault(x => x != null);
            if (game == null)
            {
                return;
            }

            try
            {
                var versions = BackupInventory.GetVersions(_settings.BackupRoot, game.Name);
                var window = _api.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowMinimizeButton = false,
                    ShowMaximizeButton = false,
                    ShowCloseButton = true,
                });

                var viewModel = new BackupFilesViewModel(
                    game.Name,
                    ResolveSavePath(game),
                    _settings.BackupRoot,
                    versions,
                    OpenFolder,
                    () => window.Close());

                window.Title = "GameBackup - 查找备份";
                window.Width = 760;
                window.Height = 560;
                window.MinWidth = 520;
                window.MinHeight = 380;
                window.Owner = _api.Dialogs.GetCurrentAppWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.Content = new BackupFilesView { DataContext = viewModel };
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                Notify("打开「" + game.Name + "」的备份文件失败：" + ex.Message, true);
            }
        }

        private void OpenFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                Notify("目录不存在：" + path, true);
                return;
            }

            Process.Start("explorer.exe", "\"" + path + "\"");
        }

        private void BackupGame(Game game)
        {
            try
            {
                if (IsCloudBackupSkipped(game.Source?.Name))
                {
                    NotifyBackup(
                        "已跳过「" + game.Name + "」：" + (game.Source?.Name ?? "该平台")
                        + " 的云存档已覆盖，设置里关闭了该平台的本地备份。");
                    return;
                }

                var savePath = ResolveSavePath(game);
                if (string.IsNullOrEmpty(savePath))
                {
                    NotifyBackup("「" + game.Name + "」还没有存档位置，请先在 GameBackup 页面查找。", true);
                    return;
                }

                var result = _backupService.Backup(
                    _settings.BackupRoot,
                    game.Name,
                    savePath,
                    DateTime.UtcNow,
                    _settings.KeepBackups);

                if (!result.Success)
                {
                    NotifyBackup("备份「" + game.Name + "」失败：" + result.Error, true);
                    return;
                }

                NotifyBackup(
                    "已备份「" + game.Name + "」：" + result.FileCount + " 个文件，"
                    + GameRowViewModel.FormatSize(result.TotalBytes));
            }
            catch (Exception ex)
            {
                NotifyBackup("备份「" + game.Name + "」失败：" + ex.Message, true);
            }
        }

        private void RestoreGame(Game game)
        {
            try
            {
                var result = _restoreService.Restore(_settings.BackupRoot, game.Name, null);

                if (!result.Success)
                {
                    NotifyBackup("恢复「" + game.Name + "」失败：" + result.Error, true);
                    return;
                }

                NotifyBackup(
                    "已恢复「" + game.Name + "」：" + result.FileCount + " 个文件，"
                    + GameRowViewModel.FormatSize(result.TotalBytes));
            }
            catch (Exception ex)
            {
                NotifyBackup("恢复「" + game.Name + "」失败：" + ex.Message, true);
            }
        }

        /// <summary>Confirmed rule first, then the deterministic locators.</summary>
        private string ResolveSavePath(Game game)
        {
            var key = "playnite:" + game.Id;

            var rule = _rules.Find(key);
            if (rule?.Saves != null && rule.Saves.Length > 0)
            {
                var saved = rule.Saves[0];
                if (!string.IsNullOrEmpty(saved) && (File.Exists(saved) || Directory.Exists(saved)))
                {
                    return saved;
                }
            }

            // A previous backup records where the game's saves really live, which
            // beats guessing when the locators have no rule for it.
            var mapped = BackupInventory.GetOriginalSavePath(_settings.BackupRoot, game.Name);
            if (!string.IsNullOrEmpty(mapped) && (File.Exists(mapped) || Directory.Exists(mapped)))
            {
                return mapped;
            }

            var descriptor = CreateDescriptor(game);
            var best = _locator.Locate(descriptor, _environment).Best;
            return best != null && best.Exists ? best.Path : null;
        }

        private static GameDescriptor CreateDescriptor(Game game)
        {
            return GameDescriptorFactory.Create(
                "playnite:" + game.Id,
                game.Name,
                game.InstallDirectory ?? string.Empty,
                game.Source?.Name ?? string.Empty,
                game.GameId ?? string.Empty,
                game.Platforms?.FirstOrDefault()?.Name ?? string.Empty,
                game.IsInstalled);
        }

        private static void Post(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.BeginInvoke(action);
            }
        }

        public string SelectFolder(string initialDirectory)
        {
            try
            {
                return string.IsNullOrWhiteSpace(initialDirectory)
                    ? _api.Dialogs.SelectFolder()
                    : _api.Dialogs.SelectFolder(initialDirectory);
            }
            catch
            {
                return null;
            }
        }

        public Task<LlmTestResult> TestLlmAsync(LlmOptions options, CancellationToken cancellationToken)
        {
            return _llmClient.TestAsync(options, cancellationToken);
        }

        private UserControl OpenPage()
        {
            if (_page == null)
            {
                _page = new GameBackupPage(_api, this);
            }

            return _page;
        }
    }
}
