using System;
using System.Collections.Generic;
using System.IO;

namespace GameBackup.Core.Scanning
{
    /// <summary>
    /// Machine-specific locations the locators probe. The host fills this in so
    /// Core stays free of registry and environment APIs.
    /// </summary>
    public sealed class SaveEnvironment
    {
        public string HomeDirectory { get; set; } = string.Empty;

        public string LocalAppData { get; set; } = string.Empty;

        public string RoamingAppData { get; set; } = string.Empty;

        public string LocalLowAppData { get; set; } = string.Empty;

        public string Documents { get; set; } = string.Empty;

        public string SavedGames { get; set; } = string.Empty;

        public string ProgramData { get; set; } = string.Empty;

        public string ProgramFiles { get; set; } = string.Empty;

        public string ProgramFilesX86 { get; set; } = string.Empty;

        /// <summary>Steam client roots, most trustworthy first. May be empty.</summary>
        public IList<string> SteamRoots { get; } = new List<string>();

        /// <summary>Directories that contain one folder per Ubisoft account.</summary>
        public IList<string> UbisoftSavegameRoots { get; } = new List<string>();

        /// <summary>GOG Galaxy application storage roots.</summary>
        public IList<string> GogApplicationRoots { get; } = new List<string>();

        /// <summary>
        /// Creates an environment from the standard Windows folders. The registry
        /// based roots are added by the host, which can access the registry.
        /// </summary>
        public static SaveEnvironment CreateDefault()
        {
            var home = SafeFolder(Environment.SpecialFolder.UserProfile);
            var environment = new SaveEnvironment
            {
                HomeDirectory = home,
                LocalAppData = SafeFolder(Environment.SpecialFolder.LocalApplicationData),
                RoamingAppData = SafeFolder(Environment.SpecialFolder.ApplicationData),
                LocalLowAppData = Combine(home, "AppData", "LocalLow"),
                Documents = SafeFolder(Environment.SpecialFolder.MyDocuments),
                SavedGames = Combine(home, "Saved Games"),
                ProgramData = SafeFolder(Environment.SpecialFolder.CommonApplicationData),
                ProgramFiles = SafeFolder(Environment.SpecialFolder.ProgramFiles),
                ProgramFilesX86 = SafeFolder(Environment.SpecialFolder.ProgramFilesX86),
            };

            environment.GogApplicationRoots.Add(
                Combine(environment.LocalAppData, "GOG.com", "Galaxy", "Applications"));

            return environment;
        }

        private static string SafeFolder(Environment.SpecialFolder folder)
        {
            try
            {
                return Environment.GetFolderPath(folder);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string Combine(params string[] parts)
        {
            var current = string.Empty;
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }
                current = string.IsNullOrEmpty(current) ? part : Path.Combine(current, part);
            }
            return current;
        }
    }
}
