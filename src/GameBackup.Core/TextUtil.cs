using System.Text.RegularExpressions;

namespace GameBackup.Core
{
    public static class TextUtil
    {
        /// <summary>
        /// netstandard2.0 does not expose the StringComparison overload of
        /// String.Replace, so paths need a small case-insensitive helper.
        /// </summary>
        public static string ReplaceIgnoreCase(string input, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(oldValue))
            {
                return input;
            }

            return Regex.Replace(
                input,
                Regex.Escape(oldValue),
                _ => newValue ?? string.Empty,
                RegexOptions.IgnoreCase);
        }
    }
}
