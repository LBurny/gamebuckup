using System;
using System.Collections.Generic;
using System.IO;
using GameBackup.Core.IO;
using GameBackup.Core.Scanning;
using Microsoft.Win32;

namespace GameBackup.Plugin.Adapters
{
    /// <summary>
    /// Builds the Core save environment from the live machine: standard folders
    /// plus the registry keys that reveal where Steam and Ubisoft Connect live.
    /// </summary>
    public static class WindowsSaveEnvironment
    {
        public static SaveEnvironment Create()
        {
            var environment = SaveEnvironment.CreateDefault();

            AddSteamRoots(environment);
            AddUbisoftRoots(environment);

            return environment;
        }

        private static void AddSteamRoots(SaveEnvironment environment)
        {
            AddSteamRoot(environment, ReadValue(
                RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamPath"));

            var steamExe = ReadValue(
                RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamExe");
            AddSteamRoot(environment, SafeDirectoryName(steamExe));

            AddSteamRoot(environment, ReadValue(
                RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Valve\Steam", "InstallPath"));
            AddSteamRoot(environment, ReadValue(
                RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"));

            AddSteamRoot(environment, Path.Combine(environment.ProgramFilesX86, "Steam"));
            AddSteamRoot(environment, Path.Combine(environment.ProgramFiles, "Steam"));
        }

        private static void AddUbisoftRoots(SaveEnvironment environment)
        {
            AddUbisoftRoot(environment, ReadValue(
                RegistryHive.LocalMachine, RegistryView.Registry32,
                @"SOFTWARE\Ubisoft\Launcher", "InstallDir"));
            AddUbisoftRoot(environment, ReadValue(
                RegistryHive.LocalMachine, RegistryView.Registry64,
                @"SOFTWARE\WOW6432Node\Ubisoft\Launcher", "InstallDir"));

            AddUbisoftRoot(environment, Path.Combine(
                environment.ProgramFilesX86, "Ubisoft", "Ubisoft Game Launcher"));
            AddUbisoftRoot(environment, Path.Combine(
                environment.ProgramFiles, "Ubisoft", "Ubisoft Game Launcher"));
            AddUbisoftRoot(environment, Path.Combine(
                environment.LocalAppData, "Ubisoft Game Launcher"));
        }

        private static void AddSteamRoot(SaveEnvironment environment, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            var root = Normalize(candidate);
            if (string.IsNullOrEmpty(root))
            {
                return;
            }

            var looksLikeSteam =
                Directory.Exists(Path.Combine(root, "steamapps"))
                || Directory.Exists(Path.Combine(root, "userdata"))
                || File.Exists(Path.Combine(root, "steam.exe"));

            if (!looksLikeSteam)
            {
                return;
            }

            if (!Contains(environment.SteamRoots, root))
            {
                environment.SteamRoots.Add(root);
            }
        }

        private static void AddUbisoftRoot(SaveEnvironment environment, string launcherDirectory)
        {
            if (string.IsNullOrWhiteSpace(launcherDirectory))
            {
                return;
            }

            var launcher = Normalize(launcherDirectory);
            if (string.IsNullOrEmpty(launcher))
            {
                return;
            }

            var savegames = Path.Combine(launcher, "savegames");
            if (!Directory.Exists(savegames))
            {
                return;
            }

            if (!Contains(environment.UbisoftSavegameRoots, savegames))
            {
                environment.UbisoftSavegameRoots.Add(savegames);
            }
        }

        private static bool Contains(IList<string> roots, string path)
        {
            foreach (var root in roots)
            {
                if (string.Equals(root, path, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static string Normalize(string path)
        {
            try
            {
                var full = Path.GetFullPath(path.Trim().Trim('"').TrimEnd('\\', '/'));
                return LongPath.DirectoryExists(full) ? full : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string SafeDirectoryName(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetDirectoryName(filePath.Trim().Replace('/', '\\'));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ReadValue(
            RegistryHive hive,
            RegistryView view,
            string subKey,
            string name)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (var key = baseKey.OpenSubKey(subKey))
                {
                    return key?.GetValue(name) as string;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
