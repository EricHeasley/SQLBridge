using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace SQLBridge {
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
            var pairs = new List<KeyValuePair<string, object?>>();
            foreach (var parameter in parameters) {
                pairs.Add(new KeyValuePair<string, object?>(parameter.ParameterName, parameter.Value));
            }

            pairs.Sort(CompareByName);

            var sb = new StringBuilder(256);
            sb.Append(commandText);

            foreach (var pair in pairs) {
                sb.Append('|');
                sb.Append(pair.Key);
                sb.Append('=');
                sb.Append(pair.Value ?? "NULL");
            }

            return $"{commandText.Split(' ', '.', '(')[0]}:{Hash(sb.ToString())}";
        }

        private static void AppendParameters(StringBuilder sb, object parameters) {
            var pairs = new List<KeyValuePair<string, object?>>();

            if (parameters is IDictionary<string, object?> dict) {
                foreach (var pair in dict) {
                    pairs.Add(pair);
                }
            } else {
                // Anonymous object / POCO
                foreach (var property in parameters.GetType().GetProperties()) {
                    pairs.Add(new KeyValuePair<string, object?>(property.Name, property.GetValue(parameters)));
                }
            }

            pairs.Sort(CompareByName);

            foreach (var pair in pairs) {
                sb.Append(pair.Key);
                sb.Append('=');
                sb.Append(pair.Value ?? "NULL");
                sb.Append('|');
            }
        }

        private static int CompareByName(KeyValuePair<string, object?> left, KeyValuePair<string, object?> right) {
            return StringComparer.OrdinalIgnoreCase.Compare(left.Key, right.Key);
        }

        private static string Hash(string input) {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes)[..16]; // 8 bytes = 16 hex chars, enough for cache keys
        }

    }
}
