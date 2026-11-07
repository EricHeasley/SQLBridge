using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace SQLBridge.Caching {
    internal static class CacheKeyBuilder {

        /// <summary>
        /// Builds a deterministic cache key from command text and parameters.
        /// Uses SHA256 to keep keys fixed-length and avoid collisions.
        /// </summary>
        public static string Build(string commandText, object? parameters) {
            var sb = new StringBuilder(256);
            sb.Append(commandText);

            if (parameters is not null) {
                sb.Append('|');
                AppendParameters(sb, parameters);
            }

            // Hash for fixed-length keys (avoids memory issues with large SQL)
            return $"{commandText.Split(' ', '.', '(')[0]}:{Hash(sb.ToString())}";
        }

        /// <summary>
        /// Builds a cache key from command text and explicit SqlParameter array.
        /// </summary>
        public static string Build(string commandText, SqlParameter[] parameters) {
            var sb = new StringBuilder(256);
            sb.Append(commandText);

            foreach (var p in parameters.OrderBy(p => p.ParameterName, StringComparer.OrdinalIgnoreCase)) {
                sb.Append('|');
                sb.Append(p.ParameterName);
                sb.Append('=');
                sb.Append(p.Value ?? "NULL");
            }

            return $"{commandText.Split(' ', '.', '(')[0]}:{Hash(sb.ToString())}";
        }

        private static void AppendParameters(StringBuilder sb, object parameters) {
            if (parameters is IDictionary<string, object?> dict) {
                foreach (var kvp in dict.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)) {
                    sb.Append(kvp.Key);
                    sb.Append('=');
                    sb.Append(kvp.Value ?? "NULL");
                    sb.Append('|');
                }
                return;
            }

            // Anonymous object / POCO
            foreach (var prop in parameters.GetType().GetProperties()
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)) {
                sb.Append(prop.Name);
                sb.Append('=');
                sb.Append(prop.GetValue(parameters) ?? "NULL");
                sb.Append('|');
            }
        }

        private static string Hash(string input) {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes)[..16]; // 8 bytes = 16 hex chars, enough for cache keys
        }

    }
}
