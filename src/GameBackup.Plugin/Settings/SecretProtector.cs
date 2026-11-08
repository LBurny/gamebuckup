using System;
using System.Security.Cryptography;
using System.Text;

namespace GameBackup.Plugin.Settings
{
    /// <summary>
    /// Encrypts the LLM API key with the current Windows user's DPAPI scope.
    /// </summary>
    public static class SecretProtector
    {
        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(plainText);
                var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(protectedBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string Unprotect(string protectedText)
        {
            if (string.IsNullOrEmpty(protectedText))
            {
                return string.Empty;
            }

            try
            {
                var bytes = Convert.FromBase64String(protectedText);
                var plainBytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
