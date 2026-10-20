using System.Text;

namespace Rth.Server.Persistence
{
    public static class PlayerNames
    {
        public const int MaxLength = 12;

        /// <summary>
        /// Cleans up a typed name for display: trims and collapses runs of spaces. Returns null
        /// if it is empty, too long, or uses anything but letters, digits, spaces, '-' and '_'.
        /// </summary>
        public static string? ToDisplayName(string? raw)
        {
            if (raw == null) return null;
            var sb = new StringBuilder();
            bool pendingSpace = false;
            foreach (char c in raw.Trim())
            {
                if (c == ' ')
                {
                    pendingSpace = true;
                    continue;
                }
                bool allowed = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!allowed) return null;
                if (pendingSpace) sb.Append(' ');
                pendingSpace = false;
                sb.Append(c);
            }
            if (sb.Length == 0 || sb.Length > MaxLength) return null;
            return sb.ToString();
        }

        /// <summary>The account key for a display name: lower case, spaces as underscores. Safe as a file name.</summary>
        public static string ToKey(string displayName) => displayName.ToLowerInvariant().Replace(' ', '_');
    }
}
