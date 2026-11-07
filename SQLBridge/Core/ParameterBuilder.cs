using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Data.SqlClient;

namespace SQLBridge.Core {
    internal static class ParameterBuilder {

        private static readonly ConcurrentDictionary<Type, PropertyAccessor[]> Cache = new();

        public static SqlParameter[] FromObject(object? parameters) {
            if (parameters is null) {
                return [];
            }

            if (parameters is SqlParameter[] array) {
                return array;
            }

            if (parameters is IEnumerable<SqlParameter> enumerable) {
                return enumerable.ToArray();
            }

            if (parameters is IDictionary<string, object?> dict) {
                return FromDictionary(dict);
            }

            var accessors = Cache.GetOrAdd(parameters.GetType(), BuildAccessors);
            return accessors
                .Select(a => new SqlParameter(EnsureAtPrefix(a.Name), a.GetValue(parameters) ?? DBNull.Value))
                .ToArray();
        }

        private static SqlParameter[] FromDictionary(IDictionary<string, object?> dict) {
            return dict
                .Select(kvp => new SqlParameter(EnsureAtPrefix(kvp.Key), kvp.Value ?? DBNull.Value))
                .ToArray();
        }

        private static PropertyAccessor[] BuildAccessors(Type type) {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .Select(p => new PropertyAccessor(p.Name, p.GetValue))
                .ToArray();
        }

        private static string EnsureAtPrefix(string name) {
            return name.StartsWith('@') ? name : $"@{name}";
        }

        private sealed record PropertyAccessor(string Name, Func<object?, object?> GetValue);

    }
}
