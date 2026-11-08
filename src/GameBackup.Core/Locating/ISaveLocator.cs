using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameBackup.Core.Models;
using GameBackup.Core.Scanning;

namespace GameBackup.Core.Locating
{
    /// <summary>
    /// A deterministic save-location source. Locators must be cheap and must
    /// never throw: the chain runs them all and keeps whatever they find.
    /// </summary>
    public interface ISaveLocator
    {
        /// <summary>Stable identifier used in logs and rule write-back.</summary>
        string Id { get; }

        IEnumerable<SaveCandidate> Locate(GameDescriptor game, SaveEnvironment environment);
    }

    internal static class CandidateFactory
    {
        public static SaveCandidate FromPath(
            string path,
            CandidateKind kind,
            double confidence,
            CandidateSource source,
            string evidence)
        {
            DateTime? lastWrite = null;
            long size = 0;

            try
            {
                if (kind == CandidateKind.File)
                {
                    if (File.Exists(path))
                    {
                        var info = new FileInfo(path);
                        size = info.Length;
                        lastWrite = info.LastWriteTimeUtc;
                    }
                }
                else if (Directory.Exists(path))
                {
                    lastWrite = Directory.GetLastWriteTimeUtc(path);
                }
            }
            catch
            {
                // Metadata is best effort only.
            }

            return new SaveCandidate
            {
                Path = path,
                Kind = kind,
                Confidence = confidence,
                Source = source,
                Evidence = evidence,
                Exists = kind == CandidateKind.File ? File.Exists(path) : Directory.Exists(path),
                SizeBytes = size,
                LastWriteUtc = lastWrite,
            };
        }

        public static bool HasFiles(string directory, int limit)
        {
            try
            {
                return Directory.EnumerateFileSystemEntries(directory)
                    .Take(limit)
                    .Any(x => File.Exists(x));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Enumerates child directories, never throwing and never following reparse points.</summary>
        public static List<string> Directories(string path, int limit)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(path))
            {
                return result;
            }

            try
            {
                foreach (var entry in Directory.EnumerateDirectories(path).Take(limit))
                {
                    try
                    {
                        var attributes = File.GetAttributes(entry);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    result.Add(entry);
                }
            }
            catch
            {
                // Missing or inaccessible directory: no candidates from it.
            }

            return result;
        }

        public static bool IsDirectory(string path)
        {
            try
            {
                return Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        public static bool MatchesStore(GameDescriptor game, params string[] tokens)
        {
            if (string.IsNullOrEmpty(game.Source))
            {
                return false;
            }

            return tokens.Any(token => NameMatching.Contains(game.Source, token));
        }

        public static bool IsNumeric(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.All(char.IsDigit);
        }
    }
}
