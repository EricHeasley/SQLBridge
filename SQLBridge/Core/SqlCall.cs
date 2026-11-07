using System.Data;

namespace SQLBridge {
    /// <summary>
    /// One command to run: its text, whether it is a stored procedure or SQL, and its parameters.
    /// </summary>
    internal sealed record SqlCall(string CommandText, CommandType CommandType, object? Parameters) {

        public static SqlCall From(string commandText, object? parameters) {
            return new SqlCall(commandText, CommandTypeResolver.Resolve(commandText), parameters);
        }

        public string CommandTypeName => CommandTypeResolver.Describe(CommandType);

    }
}
