using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GameBackup.Core.Models;

namespace GameBackup.Core.Locating
{
    /// <summary>
    /// Loose title matching used by the heuristic locators. Names are compared
    /// after stripping punctuation, spaces and common edition suffixes so
    /// "Hadean Tactics" also matches a "HadeanTactics" folder.
    /// </summary>
    internal static class NameMatching
    {
        private static readonly string[] EditionSuffixes =
        {
            "definitive edition",
            "complete edition",
            "deluxe edition",
            "enhanced edition",
            "ultimate edition",
            "special edition",
            "game of the year edition",
            "goty edition",
            "director's cut",
            "directors cut",
            "remastered",
            "remake",
        };

        public static List<string> Aliases(GameDescriptor game)
        {
            var aliases = new List<string>();

            AddVariant(aliases, game.Name);
            AddVariant(aliases, SafeLeaf(game.InstallDirectory));

            return aliases;
        }

        private static void AddVariant(List<string> aliases, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var name = value.Trim();
            AddUnique(aliases, name);

            var colon = name.IndexOf(':');
            if (colon > 1)
            {
                AddUnique(aliases, name.Substring(0, colon).Trim());
            }

            foreach (var suffix in EditionSuffixes)
            {
                if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var trimmed = name.Substring(0, name.Length - suffix.Length).Trim(' ', '-', '_', ':');
                if (trimmed.Length >= 3)
                {
                    AddUnique(aliases, trimmed);
                }
            }
        }

        private static void AddUnique(List<string> aliases, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var normalized = Normalize(value);
            if (normalized.Length < 3)
            {
                return;
            }

            foreach (var existing in aliases)
            {
                if (string.Equals(Normalize(existing), normalized, StringComparison.Ordinal))
                {
                    return;
                }
            }

            aliases.Add(value.Trim());
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(char.ToLowerInvariant(ch));
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// Returns 0 when the names do not resemble each other, 1.0 for an exact
        /// match. "Contains" matches are only allowed for reasonably long names.
        /// </summary>
        public static double Score(string candidateName, IReadOnlyList<string> aliases)
        {
            var candidate = Normalize(candidateName);
            if (candidate.Length < 4)
            {
                return 0;
            }

            var best = 0.0;

            foreach (var alias in aliases)
            {
                var expected = Normalize(alias);
                if (expected.Length < 4)
                {
                    continue;
                }

                double score;
                if (string.Equals(candidate, expected, StringComparison.Ordinal))
                {
                    score = 1.0;
                }
                else if (candidate.StartsWith(expected, StringComparison.Ordinal)
                         || expected.StartsWith(candidate, StringComparison.Ordinal))
                {
                    score = 0.9;
                }
                else if (expected.Length >= 5
                         && (candidate.IndexOf(expected, StringComparison.Ordinal) >= 0
                             || expected.IndexOf(candidate, StringComparison.Ordinal) >= 0))
                {
                    score = 0.82;
                }
                else
                {
                    score = 0;
                }

                if (score > best)
                {
                    best = score;
                }
            }

            return best;
        }

        public static bool IsSaveLikeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var normalized = Normalize(name);

            switch (normalized)
            {
                case "save":
                case "saves":
                case "savegame":
                case "savegames":
                case "savedata":
                case "savedgames":
                case "userdata":
                case "profiles":
                case "profile":
                case "storage":
                case "cloudsaves":
                    return true;
                default:
                    return false;
            }
        }

        public static string SafeLeaf(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFileName(path.TrimEnd('\\', '/')) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool Contains(string value, string token)
        {
            return !string.IsNullOrEmpty(value)
                   && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
