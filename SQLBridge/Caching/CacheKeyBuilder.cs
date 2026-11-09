using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace SQLBridge {
    internal static class CacheKeyBuilder {

        /// <summary>
        /// Builds a deterministic cache key from command text and parameters. Every key for a command starts
        /// with <see cref="InvalidationPrefix"/>, so all of its cached results can be removed together.
        /// </summary>
        public static string Build(string commandText, object? parameters) {
            var pairs = new List<KeyValuePair<string, object?>>();

            if (parameters is IDictionary<string, object?> dict) {
                foreach (var pair in dict) {
                    pairs.Add(pair);
                }
            } else if (parameters is not null) {
                // Anonymous object / POCO
                foreach (var property in parameters.GetType().GetProperties()) {
                    pairs.Add(new KeyValuePair<string, object?>(property.Name, property.GetValue(parameters)));
                }
            }

            return BuildKey(commandText, pairs);
        }

        /// <summary>
        /// Builds a cache key from command text and explicit SqlParameter array.
        /// </summary>
        public static string Build(string commandText, SqlParameter[] parameters) {
            var pairs = new List<KeyValuePair<string, object?>>();
            foreach (var parameter in parameters) {
                pairs.Add(new KeyValuePair<string, object?>(parameter.ParameterName, parameter.Value));
            }

            return BuildKey(commandText, pairs);
        }

        /// <summary>
        /// Builds a cache key for a call: command text, every input parameter value and the row type.
        /// </summary>
        public static string Build(SqlCall call, Type rowType) {
            var pairs = new List<KeyValuePair<string, object?>>();

            foreach (var parameter in ParameterBuilder.FromObject(call.Parameters)) {
                pairs.Add(new KeyValuePair<string, object?>(parameter.ParameterName, parameter.Value));
            }

            foreach (var spec in call.Specs) {
                if (spec.IsInput) {
                    pairs.Add(new KeyValuePair<string, object?>(spec.Name, spec.Value));
                }
            }

            return $"{BuildKey(call.CommandText, pairs)}:{rowType.FullName}";
        }

        /// <summary>
        /// The start shared by every cache key of a command, across all parameter values.
        /// </summary>
        public static string InvalidationPrefix(string commandText) {
            return $"{CommandKey(commandText)}:";
        }

        private static string BuildKey(string commandText, List<KeyValuePair<string, object?>> pairs) {
            pairs.Sort(CompareByName);

            var sb = new StringBuilder(256);
            foreach (var pair in pairs) {
                sb.Append(ParameterBuilder.EnsureAtPrefix(pair.Key));
                sb.Append('=');
                sb.Append(pair.Value is null || pair.Value == DBNull.Value ? "NULL" : pair.Value);
                sb.Append('|');
            }

            return $"{CommandKey(commandText)}:{Hash(sb.ToString())}";
        }

        /// <summary>
        /// A stored procedure is identified by its name; SQL text by a hash of the statement.
        /// </summary>
        private static string CommandKey(string commandText) {
            var trimmed = commandText.Trim();
            return CommandTypeResolver.Resolve(trimmed) == System.Data.CommandType.StoredProcedure
                ? trimmed
                : $"sql-{Hash(trimmed)}";
        }

        private static int CompareByName(KeyValuePair<string, object?> left, KeyValuePair<string, object?> right) {
            return StringComparer.OrdinalIgnoreCase.Compare(
                ParameterBuilder.EnsureAtPrefix(left.Key), ParameterBuilder.EnsureAtPrefix(right.Key));
        }

        private static string Hash(string input) {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes)[..16]; // 8 bytes = 16 hex chars, enough for cache keys
        }

    }
}
