using System.Data;

namespace SQLBridge {
    internal static class CommandTypeResolver {

        /// <summary>
        /// Command text without whitespace is a stored procedure name; anything else is SQL text.
        /// </summary>
        public static CommandType Resolve(string commandText) {
            foreach (var character in commandText.AsSpan().Trim()) {
                if (char.IsWhiteSpace(character)) {
                    return CommandType.Text;
                }
            }

            return CommandType.StoredProcedure;
        }

        public static string Describe(CommandType commandType) {
            return commandType == CommandType.StoredProcedure ? "StoredProcedure" : "Text";
        }

    }
}
