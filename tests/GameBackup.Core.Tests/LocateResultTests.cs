using System.Collections.Generic;
using GameBackup.Core.Models;
using Xunit;

namespace GameBackup.Core.Tests
{
    public class LocateResultTests
    {
        [Fact]
        public void Empty_result_is_unknown()
        {
            Assert.Equal(SaveStatus.Unknown, LocateResult.Empty().Status);
        }

        [Fact]
        public void Confirmed_requires_high_confidence_and_existing_path()
        {
            var result = new LocateResult
            {
                Candidates = new List<SaveCandidate>
                {
                    new SaveCandidate { Path = @"C:\Saves", Confidence = 0.95, Exists = true },
                },
            };

            Assert.Equal(SaveStatus.Confirmed, result.Status);
        }

        [Fact]
        public void High_confidence_but_missing_path_is_stale()
        {
            var result = new LocateResult
            {
                Candidates = new List<SaveCandidate>
                {
                    new SaveCandidate { Path = @"C:\Saves", Confidence = 0.95, Exists = false },
                },
            };

            Assert.Equal(SaveStatus.Stale, result.Status);
        }

        [Fact]
        public void Llm_level_confidence_needs_confirmation()
        {
            var result = new LocateResult
            {
                Candidates = new List<SaveCandidate>
                {
                    new SaveCandidate { Path = @"C:\Saves", Confidence = 0.65, Exists = true },
                },
            };

            Assert.Equal(SaveStatus.Candidate, result.Status);
        }
    }
}
