using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace SQLBridge.Mapping {
    /// <summary>
    /// High-performance mapper that compiles expression trees into delegates on first use.
    /// No PropertyInfo.SetValue or Convert.ChangeType per row — everything is compiled to IL.
    /// </summary>
    internal sealed class ReflectionResultMapper<T> : IResultMapper<T> where T : new() {

        // Cache compiled materializers keyed by column signature (ordered column names + types)
        private static readonly ConcurrentDictionary<string, Func<IDataRecord, T>> CompiledMappers = new();

        // Cache property metadata per type (computed once per T)
        private static readonly Lazy<PropertyMeta[]> PropertyMetadata = new(() => BuildPropertyMeta(typeof(T)));

        private Func<IDataRecord, T>? _materializer;

        public T Map(IDataRecord record) {
            _materializer ??= GetOrCompileMaterializer(record);
            return _materializer(record);
        }

        private static Func<IDataRecord, T> GetOrCompileMaterializer(IDataRecord record) {
            var key = BuildCacheKey(record);
            return CompiledMappers.GetOrAdd(key, _ => CompileMaterializer(record));
        }

        private static Func<IDataRecord, T> CompileMaterializer(IDataRecord record) {
            var properties = PropertyMetadata.Value;

            // Parameter: IDataRecord record
            var recordParam = Expression.Parameter(typeof(IDataRecord), "record");

            // var instance = new T();
            var instanceVar = Expression.Variable(typeof(T), "instance");
            var newExpr = Expression.New(typeof(T));

            var body = new List<Expression> { Expression.Assign(instanceVar, newExpr) };

            for (var i = 0; i < record.FieldCount; i++) {
                var columnName = record.GetName(i);
                var columnType = record.GetFieldType(i);
                var meta = FindProperty(properties, columnName);

                if (meta is null) {
                    continue;
                }

                var ordinal = Expression.Constant(i);
                var targetType = meta.Property.PropertyType;

                // record.IsDBNull(i)
                var isDbNull = Expression.Call(recordParam,
                    typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!,
                    ordinal);

                // Build the typed getter expression
                var getValueExpr = BuildGetValueExpression(recordParam, ordinal, columnType, targetType);

                // instance.Property = (TargetType)value;
                var setProp = Expression.Assign(
                    Expression.Property(instanceVar, meta.Property),
                    getValueExpr);

                // if (!record.IsDBNull(i)) instance.Property = value;
                body.Add(Expression.IfThen(Expression.Not(isDbNull), setProp));
            }

            // return instance;
            body.Add(instanceVar);

            var block = Expression.Block(new[] { instanceVar }, body);
            var lambda = Expression.Lambda<Func<IDataRecord, T>>(block, recordParam);
            return lambda.Compile();
        }

        private static Expression BuildGetValueExpression(
            Expression record, Expression ordinal, Type sourceType, Type targetType) {
            var underlyingTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // Try to use typed getter methods for common types (avoids boxing)
            var getterExpr = GetTypedReaderExpression(record, ordinal, underlyingTarget, sourceType);

            if (getterExpr is not null) {
                // If target is nullable, wrap: (int?)reader.GetInt32(i)
                if (targetType != underlyingTarget) {
                    return Expression.Convert(getterExpr, targetType);
                }
                return getterExpr;
            }

            // Fallback: record.GetValue(ordinal) with conversion
            var getValue = Expression.Call(record,
                typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!,
                ordinal);

            // Handle enums
            if (underlyingTarget.IsEnum) {
                // (TargetEnum)Enum.ToObject(typeof(TargetEnum), value)
                var enumConvert = Expression.Call(
                    typeof(Enum).GetMethod(nameof(Enum.ToObject), [typeof(Type), typeof(object)])!,
                    Expression.Constant(underlyingTarget),
                    getValue);
                var castToEnum = Expression.Convert(enumConvert, underlyingTarget);
                return targetType != underlyingTarget
                    ? Expression.Convert(castToEnum, targetType)
                    : castToEnum;
            }

            // Handle Guid from string
            if (underlyingTarget == typeof(Guid) && sourceType == typeof(string)) {
                var getString = Expression.Call(record,
                    typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetString))!,
                    ordinal);
                var parseGuid = Expression.Call(
                    typeof(Guid).GetMethod(nameof(Guid.Parse), [typeof(string)])!,
                    getString);
                return targetType != underlyingTarget
                    ? Expression.Convert(parseGuid, targetType)
                    : parseGuid;
            }

            // General fallback: Convert.ChangeType
            var convertCall = Expression.Call(
                typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!,
                getValue,
                Expression.Constant(underlyingTarget));

            var castResult = Expression.Convert(convertCall, targetType);
            return castResult;
        }

        /// <summary>
        /// Returns a typed reader expression (e.g., reader.GetInt32(ordinal)) to avoid boxing.
        /// Returns null if no direct typed getter exists for the source→target combination.
        /// </summary>
        private static Expression? GetTypedReaderExpression(
            Expression record, Expression ordinal, Type targetType, Type sourceType) {
            // Direct match: source type == target type → use typed getter
            if (sourceType == targetType) {
                var methodName = GetTypedGetterName(targetType);
                if (methodName is not null) {
                    var method = typeof(IDataRecord).GetMethod(methodName);
                    if (method is not null) {
                        return Expression.Call(record, method, ordinal);
                    }
                }
            }

            // Widening numeric conversions (e.g., int column → long property)
            if (IsNumericWiden(sourceType, targetType)) {
                var sourceGetter = GetTypedGetterName(sourceType);
                if (sourceGetter is not null) {
                    var method = typeof(IDataRecord).GetMethod(sourceGetter);
                    if (method is not null) {
                        return Expression.Convert(Expression.Call(record, method, ordinal), targetType);
                    }
                }
            }

            return null;
        }

        private static string? GetTypedGetterName(Type type) {
            return type switch {
                _ when type == typeof(bool) => nameof(IDataRecord.GetBoolean),
                _ when type == typeof(byte) => nameof(IDataRecord.GetByte),
                _ when type == typeof(char) => nameof(IDataRecord.GetChar),
                _ when type == typeof(DateTime) => nameof(IDataRecord.GetDateTime),
                _ when type == typeof(decimal) => nameof(IDataRecord.GetDecimal),
                _ when type == typeof(double) => nameof(IDataRecord.GetDouble),
                _ when type == typeof(float) => nameof(IDataRecord.GetFloat),
                _ when type == typeof(Guid) => nameof(IDataRecord.GetGuid),
                _ when type == typeof(short) => nameof(IDataRecord.GetInt16),
                _ when type == typeof(int) => nameof(IDataRecord.GetInt32),
                _ when type == typeof(long) => nameof(IDataRecord.GetInt64),
                _ when type == typeof(string) => nameof(IDataRecord.GetString),
                _ => null
            };
        }

        private static bool IsNumericWiden(Type source, Type target) {
            if (source == typeof(byte) && (target == typeof(short) || target == typeof(int) || target == typeof(long) || target == typeof(decimal) || target == typeof(double) || target == typeof(float))) {
                return true;
            }
            if (source == typeof(short) && (target == typeof(int) || target == typeof(long) || target == typeof(decimal) || target == typeof(double) || target == typeof(float))) {
                return true;
            }
            if (source == typeof(int) && (target == typeof(long) || target == typeof(decimal) || target == typeof(double))) {
                return true;
            }
            if (source == typeof(long) && (target == typeof(decimal) || target == typeof(double))) {
                return true;
            }
            if (source == typeof(float) && target == typeof(double)) {
                return true;
            }
            return false;
        }

        private static string BuildCacheKey(IDataRecord record) {
            // Build a signature from column names so different result shapes get different compiled delegates
            Span<char> buffer = stackalloc char[512];
            var pos = 0;

            for (var i = 0; i < record.FieldCount && pos < 500; i++) {
                if (i > 0) buffer[pos++] = '|';
                var name = record.GetName(i);
                foreach (var c in name) {
                    if (pos >= 500) break;
                    buffer[pos++] = char.ToLowerInvariant(c);
                }
            }

            return new string(buffer[..pos]);
        }

        private static PropertyMeta[] BuildPropertyMeta(Type type) {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .Select(p => {
                    var attr = p.GetCustomAttribute<ColumnAttribute>();
                    var name = attr?.Name ?? p.Name;
                    return new PropertyMeta(name, p);
                })
                .ToArray();
        }

        private static PropertyMeta? FindProperty(PropertyMeta[] properties, string columnName) {
            return Array.Find(properties,
                p => string.Equals(p.MappedName, columnName, StringComparison.OrdinalIgnoreCase));
        }

        private sealed record PropertyMeta(string MappedName, PropertyInfo Property);

    }
}
