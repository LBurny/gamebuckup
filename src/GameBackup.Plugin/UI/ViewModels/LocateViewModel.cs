using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GameBackup.Core.IO;
using GameBackup.Core.Locating;
using GameBackup.Core.Models;
using GameBackup.Plugin.UI.Infrastructure;

namespace GameBackup.Plugin.UI.ViewModels
{
    public sealed class CandidateViewModel
    {
        public CandidateViewModel(SaveCandidate model)
        {
            Model = model;
        }

        public SaveCandidate Model { get; }

        public string Path => Model.Path;

        public string Reason => string.IsNullOrWhiteSpace(Model.Evidence) ? "自动识别结果" : Model.Evidence;

        public string SourceText => SourceLabels.Describe(Model.Source);

        public string KindText => Model.Kind == CandidateKind.File ? "文件" : "文件夹";

        public string ConfidenceText => "置信度 " + (int)Math.Round(Model.Confidence * 100) + "%";

        public string ExistsText => Model.Exists ? "已确认存在" : "路径不存在";
    }

    public sealed class LocateViewModel : Infrastructure.ObservableObject
    {
        private readonly string _installDirectory;
        private readonly Func<CancellationToken, Task<LocateReport>> _locate;
        private readonly Action<SaveCandidate> _apply;
        private readonly Action _close;

        private CancellationTokenSource _cancellation;
        private bool _isBusy;
        private string _statusText = string.Empty;
        private string _manualPath = string.Empty;

        public LocateViewModel(
            string gameName,
            string installDirectory,
            Func<CancellationToken, Task<LocateReport>> locate,
            Action<SaveCandidate> apply,
            Action close)
        {
            GameName = gameName ?? string.Empty;
            _installDirectory = installDirectory ?? string.Empty;
            _locate = locate;
            _apply = apply;
            _close = close;

            ApplyCommand = new RelayCommand(
                parameter => Apply(parameter as CandidateViewModel));
            ManualApplyCommand = new RelayCommand(_ => ApplyManual());
            RetryCommand = new RelayCommand(_ => Start());
            CancelCommand = new RelayCommand(_ => CloseDialog());
        }

        public string GameName { get; }

        public string InstallDirectory => _installDirectory;

        public ObservableCollection<CandidateViewModel> Candidates { get; } =
            new ObservableCollection<CandidateViewModel>();

        public ICommand ApplyCommand { get; }

        public ICommand ManualApplyCommand { get; }

        public ICommand RetryCommand { get; }

        public ICommand CancelCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (Set(ref _isBusy, value))
                {
                    RaisePropertyChanged(nameof(HasCandidates));
                    RaisePropertyChanged(nameof(ShowEmptyHint));
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set => Set(ref _statusText, value);
        }

        public string ManualPath
        {
            get => _manualPath;
            set => Set(ref _manualPath, value);
        }

        public bool HasCandidates => !IsBusy && Candidates.Count > 0;

        public bool ShowEmptyHint => !IsBusy && Candidates.Count == 0;

        public void Start()
        {
            if (IsBusy)
            {
                return;
            }

            _ = RunAsync();
        }

        /// <summary>Stops a running request without closing the host view.</summary>
        public void Stop()
        {
            Cancel();
        }

        private async Task RunAsync()
        {
            Cancel();
            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;

            Candidates.Clear();
            RaisePropertyChanged(nameof(HasCandidates));
            RaisePropertyChanged(nameof(ShowEmptyHint));

            IsBusy = true;
            StatusText = "正在检查 Steam、育碧等平台目录与常见存档位置…";

            try
            {
                var report = await _locate(token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                var found = report?.Candidates ?? new List<SaveCandidate>();
                foreach (var candidate in found)
                {
                    Candidates.Add(new CandidateViewModel(candidate));
                }

                if (!string.IsNullOrWhiteSpace(report?.Error))
                {
                    StatusText = found.Count == 0
                        ? "本地没有命中，AI 调用失败：" + report.Error
                        : $"本地找到 {found.Count} 个候选；AI 调用失败：{report.Error}";
                }
                else if (found.Count == 0)
                {
                    StatusText = string.IsNullOrWhiteSpace(report?.Note)
                        ? "没有找到确定存在的存档路径。可以手动指定，或再试一次。"
                        : report.Note;
                }
                else
                {
                    StatusText = $"找到 {found.Count} 个候选位置，请确认。"
                                 + (string.IsNullOrWhiteSpace(report?.Note) ? string.Empty : " " + report.Note);
                }
            }
            catch (OperationCanceledException)
            {
                StatusText = "已取消。";
            }
            catch (Exception ex)
            {
                StatusText = "定位失败：" + ex.Message;
            }
            finally
            {
                IsBusy = false;
                RaisePropertyChanged(nameof(HasCandidates));
                RaisePropertyChanged(nameof(ShowEmptyHint));
            }
        }

        private void Apply(CandidateViewModel candidate)
        {
            if (candidate == null)
            {
                return;
            }

            Cancel();
            _apply(candidate.Model);
            _close();
        }

        private void ApplyManual()
        {
            var path = (ManualPath ?? string.Empty).Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path))
            {
                StatusText = "请先填写一个路径。";
                return;
            }

            try
            {
                path = Path.GetFullPath(path);
            }
            catch
            {
                StatusText = "路径格式无效。";
                return;
            }

            var isDirectory = LongPath.DirectoryExists(path);
            var isFile = LongPath.FileExists(path);
            if (!isDirectory && !isFile)
            {
                StatusText = "这个路径不存在，请确认后再试。";
                return;
            }

            Cancel();
            _apply(new SaveCandidate
            {
                Path = path,
                Kind = isFile ? CandidateKind.File : CandidateKind.Directory,
                Confidence = 1.0,
                Source = CandidateSource.Manual,
                Evidence = "手动指定",
                Exists = true,
            });
            _close();
        }

        /// <summary>
        /// Aborts the running request and asks the host to hide the dialog.
        /// Kept separate from <see cref="Cancel"/> so the host-initiated
        /// <see cref="Stop"/> does not bounce back into its own close handler.
        /// </summary>
        private void CloseDialog()
        {
            Cancel();
            _close();
        }

        private void Cancel()
        {
            if (_cancellation != null)
            {
                try
                {
                    _cancellation.Cancel();
                }
                catch
                {
                    // Ignore cancellation races.
                }
            }
        }
    }
}
