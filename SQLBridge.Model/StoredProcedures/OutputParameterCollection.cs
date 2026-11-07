using Microsoft.Data.SqlClient;

namespace SQLBridge.Model.StoredProcedures {
    public sealed class OutputParameterCollection {

        private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

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

        public object? GetRaw(string parameterName) {
            var key = parameterName.StartsWith('@') ? parameterName : $"@{parameterName}";
            return _values.TryGetValue(key, out var value) ? value : null;
        }

        public int? ReturnValue => _values.TryGetValue("@RETURN_VALUE", out var v) ? (int?)v : null;

        public bool HasParameter(string parameterName) {
            var key = parameterName.StartsWith('@') ? parameterName : $"@{parameterName}";
            return _values.ContainsKey(key);
        }

    }
}
