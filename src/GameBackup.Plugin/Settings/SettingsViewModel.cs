using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GameBackup.Core.Llm;
using GameBackup.Plugin.Settings;
using GameBackup.Plugin.UI.Infrastructure;

namespace GameBackup.Plugin.Settings
{
    public sealed class SettingsViewModel : ObservableObject
    {
        private readonly IGameBackupHost _host;
        private readonly Action _close;
        private string _backupRoot;
        private bool _autoBackupOnExit;
        private bool _askOnGameExit;
        private BackupNotificationMode _notification = BackupNotificationMode.Always;
        private string _keepBackupsText;
        private bool _skipCloudSyncedBackups;
        private readonly System.Collections.Generic.HashSet<string> _cloudPlatforms =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _enableLlm;
        private string _llmEndpoint;
        private string _llmModel;
        private string _apiKey;
        private string _statusText = string.Empty;

        public SettingsViewModel(IGameBackupHost host, Action close)
        {
            _host = host;
            _close = close;

            BrowseCommand = new RelayCommand(Browse);
            SaveCommand = new RelayCommand(Save);
            CloseCommand = new RelayCommand(Close);
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);

            Load();
        }

        public event EventHandler Closed;

        public ICommand BrowseCommand { get; }

        public ICommand SaveCommand { get; }

        public ICommand CloseCommand { get; }

        public ICommand TestConnectionCommand { get; }

        public string BackupRoot
        {
            get => _backupRoot;
            set => Set(ref _backupRoot, value);
        }

        public bool AutoBackupOnExit
        {
            get => _autoBackupOnExit;
            set => Set(ref _autoBackupOnExit, value);
        }

        public bool AskOnGameExit
        {
            get => _askOnGameExit;
            set => Set(ref _askOnGameExit, value);
        }

        public bool NotifyAlways
        {
            get => _notification == BackupNotificationMode.Always;
            set
            {
                if (value)
                {
                    _notification = BackupNotificationMode.Always;
                    RaisePropertyChanged(nameof(NotifyAlways));
                    RaisePropertyChanged(nameof(NotifyFailuresOnly));
                }
            }
        }

        public bool NotifyFailuresOnly
        {
            get => _notification == BackupNotificationMode.FailuresOnly;
            set
            {
                if (value)
                {
                    _notification = BackupNotificationMode.FailuresOnly;
                    RaisePropertyChanged(nameof(NotifyAlways));
                    RaisePropertyChanged(nameof(NotifyFailuresOnly));
                }
            }
        }

        public string KeepBackupsText
        {
            get => _keepBackupsText;
            set => Set(ref _keepBackupsText, value);
        }

        public bool SkipCloudSyncedBackups
        {
            get => _skipCloudSyncedBackups;
            set => Set(ref _skipCloudSyncedBackups, value);
        }

        public bool SkipEpic
        {
            get => IsCloudPlatformSelected("Epic");
            set => SetCloudPlatform("Epic", nameof(SkipEpic), value);
        }

        public bool SkipGog
        {
            get => IsCloudPlatformSelected("GOG");
            set => SetCloudPlatform("GOG", nameof(SkipGog), value);
        }

        public bool SkipOrigin
        {
            get => IsCloudPlatformSelected("Origin");
            set => SetCloudPlatform("Origin", nameof(SkipOrigin), value);
        }

        public bool SkipSteam
        {
            get => IsCloudPlatformSelected("Steam");
            set => SetCloudPlatform("Steam", nameof(SkipSteam), value);
        }

        public bool SkipUbisoft
        {
            get => IsCloudPlatformSelected("Ubisoft");
            set => SetCloudPlatform("Ubisoft", nameof(SkipUbisoft), value);
        }

        public bool EnableLlm
        {
            get => _enableLlm;
            set => Set(ref _enableLlm, value);
        }

        public string LlmEndpoint
        {
            get => _llmEndpoint;
            set => Set(ref _llmEndpoint, value);
        }

        public string LlmModel
        {
            get => _llmModel;
            set => Set(ref _llmModel, value);
        }

        public string ApiKey
        {
            get => _apiKey;
            set => Set(ref _apiKey, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => Set(ref _statusText, value);
        }

        public void Load()
        {
            var settings = _host.Settings;
            BackupRoot = settings.BackupRoot;
            AutoBackupOnExit = settings.AutoBackupOnExit;
            AskOnGameExit = settings.AskOnGameExit;
            _notification = settings.BackupNotification;
            RaisePropertyChanged(nameof(NotifyAlways));
            RaisePropertyChanged(nameof(NotifyFailuresOnly));
            KeepBackupsText = settings.KeepBackups.ToString();
            SkipCloudSyncedBackups = settings.SkipCloudSyncedBackups;
            _cloudPlatforms.Clear();
            foreach (var key in settings.SkipCloudPlatforms ?? new System.Collections.Generic.List<string>())
            {
                _cloudPlatforms.Add(key);
            }
            RaiseCloudPlatformChanged();
            EnableLlm = settings.EnableLlm;
            LlmEndpoint = settings.LlmEndpoint;
            LlmModel = settings.LlmModel;
            ApiKey = SecretProtector.Unprotect(settings.LlmApiKeyProtected);
            StatusText = string.Empty;
        }

        private bool IsCloudPlatformSelected(string key)
        {
            return _cloudPlatforms.Contains(key);
        }

        private void SetCloudPlatform(string key, string propertyName, bool selected)
        {
            var changed = selected ? _cloudPlatforms.Add(key) : _cloudPlatforms.Remove(key);
            if (changed)
            {
                RaisePropertyChanged(propertyName);
            }
        }

        private void RaiseCloudPlatformChanged()
        {
            RaisePropertyChanged(nameof(SkipEpic));
            RaisePropertyChanged(nameof(SkipGog));
            RaisePropertyChanged(nameof(SkipOrigin));
            RaisePropertyChanged(nameof(SkipSteam));
            RaisePropertyChanged(nameof(SkipUbisoft));
        }

        private void Browse()
        {
            var selected = _host.SelectFolder(BackupRoot);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                BackupRoot = selected;
            }
        }

        private void Save()
        {
            if (!int.TryParse(KeepBackupsText, out var keep) || keep < 1 || keep > 100)
            {
                StatusText = "保留份数需要是 1 到 100 之间的整数。";
                return;
            }

            if (string.IsNullOrWhiteSpace(BackupRoot))
            {
                StatusText = "请选择备份目录。";
                return;
            }

            if (SkipCloudSyncedBackups && _cloudPlatforms.Count == 0)
            {
                StatusText = "请至少勾选一个云存档平台，或关闭「不要备份云支持的游戏」。";
                return;
            }

            var settings = _host.Settings;
            settings.BackupRoot = BackupRoot.Trim();
            settings.AutoBackupOnExit = AutoBackupOnExit;
            settings.AskOnGameExit = AskOnGameExit;
            settings.BackupNotification = _notification;
            settings.KeepBackups = keep;
            settings.SkipCloudSyncedBackups = SkipCloudSyncedBackups;
            settings.SkipCloudPlatforms = GameBackup.Core.Backup.CloudPlatforms.All
                .Where(platform => _cloudPlatforms.Contains(platform.Key))
                .Select(platform => platform.Key)
                .ToList();
            settings.EnableLlm = EnableLlm;
            settings.LlmEndpoint = (LlmEndpoint ?? string.Empty).Trim();
            settings.LlmModel = (LlmModel ?? string.Empty).Trim();
            settings.LlmApiKeyProtected = SecretProtector.Protect(ApiKey);

            _host.SaveSettings(settings);
            StatusText = "已保存。";
        }

        private void Close()
        {
            _close();
            Closed?.Invoke(this, EventArgs.Empty);
        }

        private async Task TestConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(LlmEndpoint) || string.IsNullOrWhiteSpace(LlmModel))
            {
                StatusText = "请先填写服务地址和模型。";
                return;
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                StatusText = "请先填写 API Key。";
                return;
            }

            StatusText = "正在测试连接…";

            var options = new LlmOptions
            {
                Endpoint = LlmEndpoint.Trim(),
                Model = LlmModel.Trim(),
                ApiKey = ApiKey,
                TimeoutSeconds = 45,
            };

            try
            {
                var result = await _host.TestLlmAsync(options, CancellationToken.None);
                if (result.Success)
                {
                    StatusText = $"连接成功 · {result.Model} · {result.Latency.TotalMilliseconds:0} ms";
                }
                else
                {
                    StatusText = "连接失败：" + result.Message;
                }
            }
            catch (Exception ex)
            {
                StatusText = "连接失败：" + ex.Message;
            }
        }
    }
}
