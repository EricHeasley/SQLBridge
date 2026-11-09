using System.Data;
using Microsoft.Data.SqlClient;

namespace SQLBridge {
    /// <summary>
    /// A parameter described as data, so a fresh <see cref="SqlParameter"/> is built for every command
    /// (a SqlParameter cannot be reused across commands, and retries create new commands).
    /// </summary>
    internal sealed record ParameterSpec(
        string Name,
        object? Value,
        SqlDbType? DbType,
        int Size,
        ParameterDirection Direction) {

        public static ParameterSpec Input(string name, object? value) {
            return new ParameterSpec(ParameterBuilder.EnsureAtPrefix(name), value, null, 0, ParameterDirection.Input);
        }

        public static ParameterSpec FromParameter(SqlParameter parameter) {
            return new ParameterSpec(
                ParameterBuilder.EnsureAtPrefix(parameter.ParameterName),
                parameter.Value == DBNull.Value ? null : parameter.Value,
                parameter.SqlDbType,
                parameter.Size,
                parameter.Direction);
        }

        public bool IsInput => Direction is ParameterDirection.Input or ParameterDirection.InputOutput;

        public SqlParameter ToSqlParameter() {
            SqlParameter parameter;
            if (DbType is { } dbType) {
                parameter = Size > 0 ? new SqlParameter(Name, dbType, Size) : new SqlParameter(Name, dbType);
            } else {
                parameter = new SqlParameter { ParameterName = Name };
            }

            parameter.Direction = Direction;
            if (IsInput) {
                parameter.Value = Value ?? DBNull.Value;
            }

            return parameter;
        }

    }
}
