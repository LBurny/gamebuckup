using System.Collections.Generic;
using System.Linq;

namespace GameBackup.Core.Models
{
    /// <summary>
    /// The outcome of a save-location resolution pass.
    /// </summary>
    public sealed class LocateResult
    {
        public LocateResult()
        {
            Candidates = new List<SaveCandidate>();
        }

        public IList<SaveCandidate> Candidates { get; set; }

        public bool NeedsConfirmation { get; set; }

        public SaveCandidate Best
        {
            get
            {
                return Candidates
                    .OrderByDescending(x => x.Confidence)
                    .ThenByDescending(x => x.Exists)
                    .FirstOrDefault();
            }
        }

        public SaveStatus Status
        {
            get
            {
                var best = Best;
                if (best == null)
                {
                    return SaveStatus.Unknown;
                }
                if (best.Confidence < 0.85)
                {
                    return SaveStatus.Candidate;
                }
                return best.Exists ? SaveStatus.Confirmed : SaveStatus.Stale;
            }
        }

        public static LocateResult Empty()
        {
            return new LocateResult { NeedsConfirmation = false };
        }
    }
}
