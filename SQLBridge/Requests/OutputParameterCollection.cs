using Microsoft.Data.SqlClient;

namespace SQLBridge {
    /// <summary>
    /// Output, input/output and return value parameters captured after a command runs.
    /// Names are case-insensitive and the "@" prefix is optional.
    /// </summary>
    public sealed class OutputParameterCollection {

        private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Copies output-direction parameters from an executed command.</summary>
        public void Capture(SqlParameterCollection parameters) {
            foreach (SqlParameter param in parameters) {
                if (param.Direction is System.Data.ParameterDirection.Output
                    or System.Data.ParameterDirection.InputOutput
                    or System.Data.ParameterDirection.ReturnValue) {
                    var value = param.Value == DBNull.Value ? null : param.Value;
                    _values[param.ParameterName] = value;
                }
            }
        }

        /// <summary>Gets an output value converted to <typeparamref name="T"/>; default when it is <c>DBNull</c>.</summary>
        /// <exception cref="KeyNotFoundException">The parameter was not an output parameter.</exception>
        public T? Get<T>(string parameterName) {
            var key = parameterName.StartsWith('@') ? parameterName : $"@{parameterName}";

            if (!_values.TryGetValue(key, out var value)) {
                throw new KeyNotFoundException($"Output parameter '{key}' was not found.");
            }

            if (value is null) {
                return default;
            }

            return (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
        }

        /// <summary>Gets the unconverted value, or null when missing or <c>DBNull</c>.</summary>
        public object? GetRaw(string parameterName) {
            var key = parameterName.StartsWith('@') ? parameterName : $"@{parameterName}";
            return _values.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>The procedure's return value, when captured with <c>WithReturnValue()</c>.</summary>
        public int? ReturnValue => _values.TryGetValue("@RETURN_VALUE", out var v) ? (int?)v : null;

        /// <summary>True when the named parameter was captured.</summary>
        public bool HasParameter(string parameterName) {
            var key = parameterName.StartsWith('@') ? parameterName : $"@{parameterName}";
            return _values.ContainsKey(key);
        }

    }
}
