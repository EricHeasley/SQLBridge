using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Data.SqlClient;

namespace SQLBridge {
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
            var result = new List<SqlParameter>();
            foreach (var accessor in accessors) {
                result.Add(new SqlParameter(EnsureAtPrefix(accessor.Name), accessor.GetValue(parameters) ?? DBNull.Value));
            }

            return result.ToArray();
        }

        private static SqlParameter[] FromDictionary(IDictionary<string, object?> dict) {
            var result = new List<SqlParameter>();
            foreach (var pair in dict) {
                result.Add(new SqlParameter(EnsureAtPrefix(pair.Key), pair.Value ?? DBNull.Value));
            }

            return result.ToArray();
        }

        private static PropertyAccessor[] BuildAccessors(Type type) {
            var accessors = new List<PropertyAccessor>();
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                if (property.CanRead) {
                    accessors.Add(new PropertyAccessor(property.Name, property.GetValue));
                }
            }

            return accessors.ToArray();
        }

        private static string EnsureAtPrefix(string name) {
            return name.StartsWith('@') ? name : $"@{name}";
        }

        private sealed record PropertyAccessor(string Name, Func<object?, object?> GetValue);

    }
}
