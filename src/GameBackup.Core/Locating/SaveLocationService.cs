using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameBackup.Core.Llm;
using GameBackup.Core.Models;
using GameBackup.Core.Scanning;

namespace GameBackup.Core.Locating
{
    /// <summary>
    /// Result of a full resolution pass, including why the AI fallback did or did
    /// not run. The UI shows <see cref="Note"/> and <see cref="Error"/> to the user.
    /// </summary>
    public sealed class LocateReport
    {
        public IList<SaveCandidate> Candidates { get; set; } = new List<SaveCandidate>();

        public string Note { get; set; }

        public string Error { get; set; }
    }

    /// <summary>
    /// The location chain: deterministic store/root locators first, LLM only when
    /// nothing trustworthy was found. This keeps the common platform games free
    /// of AI calls and leaves the model for manual installs.
    /// </summary>
    public sealed class SaveLocationService
    {
        private readonly SaveLocator _locator;
        private readonly LlmClient _llm;
        private readonly string _homeDirectory;

        public SaveLocationService(SaveLocator locator, LlmClient llm, string homeDirectory)
        {
            _locator = locator ?? new SaveLocator(new ISaveLocator[0]);
            _llm = llm;
            _homeDirectory = (homeDirectory ?? string.Empty).TrimEnd('\\', '/');
        }

        public LocateResult LocateOffline(GameDescriptor game, SaveEnvironment environment)
        {
            return _locator.Locate(game, environment);
        }

        public async Task<LocateReport> LocateAsync(
            GameDescriptor game,
            SaveEnvironment environment,
            LlmOptions llmOptions,
            string llmSkipReason,
            CancellationToken cancellationToken)
        {
            var local = await Task.Run(() => LocateOffline(game, environment), cancellationToken)
                .ConfigureAwait(false);
            var report = new LocateReport { Candidates = local.Candidates };

            if (local.Candidates.Any(
                    x => x.Exists && x.Confidence >= SaveLocator.AutoConfirmThreshold))
            {
                return report;
            }

            if (llmOptions == null || _llm == null)
            {
                report.Note = string.IsNullOrWhiteSpace(llmSkipReason)
                    ? "AI 未启用，仅使用了本地识别结果。"
                    : llmSkipReason;
                return report;
            }

            try
            {
                var ai = await _llm
                    .LocateAsync(game, _homeDirectory, llmOptions, cancellationToken)
                    .ConfigureAwait(false);

                report.Candidates = Merge(local.Candidates, ai);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                report.Error = ex.Message;
            }

            return report;
        }

        private static IList<SaveCandidate> Merge(
            IEnumerable<SaveCandidate> local,
            IEnumerable<SaveCandidate> ai)
        {
            var merged = new Dictionary<string, SaveCandidate>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in local.Concat(ai ?? new SaveCandidate[0]))
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.Path))
                {
                    continue;
                }

                var key = NormalizePath(candidate.Path);
                if (merged.TryGetValue(key, out var existing))
                {
                    if (candidate.Confidence > existing.Confidence)
                    {
                        merged[key] = candidate;
                    }
                    continue;
                }

                merged[key] = candidate;
            }

            return merged.Values
                .OrderByDescending(x => x.Confidence)
                .ThenByDescending(x => x.Exists)
                .ToList();
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path).TrimEnd('\\', '/');
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}
