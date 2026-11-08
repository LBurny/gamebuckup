using System;
using System.Security.Cryptography;
using System.Text;
using GameBackup.Core.Models;

namespace GameBackup.Core
{
    public static class GameDescriptorFactory
    {
        public static GameDescriptor Create(
            string key,
            string name,
            string installDirectory,
            string source,
            string storeGameId,
            string platform,
            bool isInstalled)
        {
            var raw = string.Join(
                "|",
                key ?? string.Empty,
                name ?? string.Empty,
                NormalizePath(installDirectory),
                storeGameId ?? string.Empty);

            return new GameDescriptor
            {
                Key = key,
                Name = name ?? string.Empty,
                InstallDirectory = installDirectory ?? string.Empty,
                Source = source ?? string.Empty,
                StoreGameId = storeGameId ?? string.Empty,
                Platform = platform ?? string.Empty,
                IsInstalled = isInstalled,
                Fingerprint = Sha1Hex(raw),
            };
        }

        /// <summary>
        /// Normalizes separators, trailing separators and case so the same install
        /// directory always produces the same fingerprint on Windows.
        /// </summary>
        public static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
        }

        private static string Sha1Hex(string value)
        {
            using (var sha1 = SHA1.Create())
            {
                var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
