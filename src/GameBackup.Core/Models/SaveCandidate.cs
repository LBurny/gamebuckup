using System;
using System.Collections.Generic;

namespace GameBackup.Core.Models
{
    /// <summary>
    /// One possible save location, with the evidence that produced it.
    /// </summary>
    public sealed class SaveCandidate
    {
        public string Path { get; set; }

        public CandidateKind Kind { get; set; }

        /// <summary>0..1. LLM-only candidates are capped well below the auto-accept threshold.</summary>
        public double Confidence { get; set; }

        public CandidateSource Source { get; set; }

        /// <summary>Human-readable reason shown in the confirmation UI.</summary>
        public string Evidence { get; set; }

        public IList<string> Tags { get; set; } = new List<string>();

        /// <summary>Whether the path was verified on disk at scan time.</summary>
        public bool Exists { get; set; }

        public long SizeBytes { get; set; }

        public DateTime? LastWriteUtc { get; set; }
    }
}
