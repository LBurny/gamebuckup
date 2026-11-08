using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GameBackup.Core.Models;
using GameBackup.Core.Scanning;

namespace GameBackup.Core.Llm
{
    public sealed class LlmContext
    {
        public string SystemPrompt { get; set; }

        public string UserPrompt { get; set; }
    }

    /// <summary>
    /// Builds a bounded, redacted description of the game and its surroundings.
    /// Only file and directory names are sent; file contents never are.
    /// </summary>
    public static class LlmContextBuilder
    {
        private const string HomePlaceholder = "<home>";

        public static LlmContext Build(GameDescriptor game, string homeDirectory)
        {
            var home = (homeDirectory ?? string.Empty).TrimEnd('\\', '/');
            var builder = new StringBuilder();

            builder.AppendLine("Game name: " + (game.Name ?? string.Empty));
            builder.AppendLine("Store: " + (string.IsNullOrEmpty(game.Source) ? "unknown" : game.Source));
            builder.AppendLine("Store id: " + (string.IsNullOrEmpty(game.StoreGameId) ? "unknown" : game.StoreGameId));
            builder.AppendLine("Install directory: " + Redact(game.InstallDirectory, home));

            var installListing = BoundedDirectoryLister.List(game.InstallDirectory, 2, 200);
            builder.AppendLine();
            builder.AppendLine("Install directory listing (relative, depth <= 2):");
            AppendListing(builder, installListing, home);

            var roots = new[]
            {
                Path.Combine(home, "Documents", "My Games"),
                Path.Combine(home, "Saved Games"),
                Path.Combine(home, "AppData", "Roaming"),
                Path.Combine(home, "AppData", "Local"),
                Path.Combine(home, "AppData", "LocalLow"),
                Path.Combine(home, "Documents"),
            };

            var tokens = BuildTokens(game);

            foreach (var root in roots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                builder.AppendLine();
                builder.AppendLine("Save root: " + Redact(root, home));
                AppendListing(builder, BuildSaveRootListing(root, tokens), home);
            }

            var userPrompt = TextUtil.ReplaceIgnoreCase(builder.ToString(), home, HomePlaceholder);

            const string systemPrompt =
                "You are a Windows PC game save-location expert. " +
                "Identify where a game stores its save files, using ONLY the directory listings in the user message. " +
                "Return strict JSON and nothing else, in this exact shape: " +
                "{\"candidates\":[{\"path\":\"<home>\\\\Documents\\\\My Games\\\\Example\\\\Saves\",\"kind\":\"dir\",\"confidence\":0.82,\"reason\":\"...\"}]}. " +
                "Rules: at most 4 candidates; use \"<home>\" instead of the user's home directory; " +
                "only return paths that exist in the listings or are a listed directory plus a save-like file name; " +
                "kind is \"dir\" or \"file\"; confidence is between 0 and 1; " +
                "return {\"candidates\":[]} when there is not enough evidence. Do not use markdown.";

            return new LlmContext
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
            };
        }

        private static void AppendListing(StringBuilder builder, IReadOnlyList<string> entries, string home)
        {
            if (entries.Count == 0)
            {
                builder.AppendLine("  (empty)");
                return;
            }

            foreach (var entry in entries)
            {
                builder.AppendLine("  " + Redact(entry, home));
            }
        }

        /// <summary>
        /// Lists the top level of a save root and expands only the branches whose
        /// names overlap with the game, so a 100-entry AppData folder cannot crowd
        /// the real candidate out of the prompt budget.
        /// </summary>
        private static IReadOnlyList<string> BuildSaveRootListing(string root, HashSet<string> tokens)
        {
            const int maxTopLevel = 150;
            const int maxChildren = 25;
            const int maxGrandChildren = 12;

            var lines = new List<string>();

            List<string> topLevel;
            try
            {
                topLevel = Directory.EnumerateFileSystemEntries(root)
                    .OrderBy(x => IsDirectory(x) ? 0 : 1)
                    .ThenBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase)
                    .Take(maxTopLevel)
                    .ToList();
            }
            catch
            {
                return lines;
            }

            foreach (var entry in topLevel)
            {
                var name = Path.GetFileName(entry);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var isDirectory = IsDirectory(entry);
                lines.Add(isDirectory ? name + "/" : name);

                if (!isDirectory)
                {
                    continue;
                }

                List<string> children;
                try
                {
                    children = Directory.EnumerateFileSystemEntries(entry)
                        .Take(maxChildren)
                        .ToList();
                }
                catch
                {
                    continue;
                }

                var relevant = Matches(name, tokens)
                               || children.Any(x => Matches(Path.GetFileName(x), tokens));

                if (!relevant)
                {
                    continue;
                }

                foreach (var child in children)
                {
                    var childName = Path.GetFileName(child);
                    var childIsDirectory = IsDirectory(child);
                    lines.Add("  " + name + "/" + childName + (childIsDirectory ? "/" : string.Empty));

                    if (!childIsDirectory || !Matches(childName, tokens))
                    {
                        continue;
                    }

                    try
                    {
                        foreach (var grandChild in Directory.EnumerateFileSystemEntries(child).Take(maxGrandChildren))
                        {
                            var grandChildName = Path.GetFileName(grandChild);
                            lines.Add("    " + name + "/" + childName + "/" + grandChildName
                                      + (IsDirectory(grandChild) ? "/" : string.Empty));
                        }
                    }
                    catch
                    {
                        // Access denied or transient IO: keep the listing we have.
                    }
                }
            }

            return lines;
        }

        private static HashSet<string> BuildTokens(GameDescriptor game)
        {
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "the", "and", "for", "with", "of", "a", "an",
                "game", "games", "edition", "definitive", "remastered",
                "complete", "deluxe", "windows", "steam", "pc",
            };

            var installLeaf = string.Empty;
            try
            {
                installLeaf = Path.GetFileName(
                    (game.InstallDirectory ?? string.Empty).TrimEnd('\\', '/'));
            }
            catch
            {
                // Ignore malformed paths.
            }

            var text = (game.Name ?? string.Empty) + " " + installLeaf;
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var part in text.Split(
                         new[] { ' ', '-', '_', ':', '.', '(', ')', '[', ']', '\'', ',', '/', '\\', '+', '&' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var token = part.Trim();
                if (token.Length >= 3 && !stopWords.Contains(token))
                {
                    tokens.Add(token);
                }
            }

            return tokens;
        }

        private static bool Matches(string name, HashSet<string> tokens)
        {
            if (string.IsNullOrEmpty(name) || tokens.Count == 0)
            {
                return false;
            }

            foreach (var token in tokens)
            {
                if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDirectory(string path)
        {
            try
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }
                return (attributes & FileAttributes.Directory) != 0;
            }
            catch
            {
                return false;
            }
        }

        private static string Redact(string value, string home)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(home))
            {
                return value ?? string.Empty;
            }
            return TextUtil.ReplaceIgnoreCase(value ?? string.Empty, home, HomePlaceholder);
        }
    }
}
