using System.Data;

namespace SQLBridge {
    /// <summary>
    /// One command to run: its text, whether it is a stored procedure or SQL, and everything the caller
    /// configured for it (parameters, timeout, mappers).
    /// </summary>
    /// <param name="CommandText">Stored procedure name or SQL text.</param>
    /// <param name="CommandType">Resolved from the command text.</param>
    /// <param name="Parameters">Parameters passed as an object (anonymous object, dictionary, SqlParameter array), or null.</param>
    /// <param name="Specs">Parameters added one by one through a request.</param>
    /// <param name="TimeoutSeconds">Per-call command timeout, or null for the default.</param>
    /// <param name="Mappers">Row mappers chosen for this call, keyed by row type.</param>
    internal sealed record SqlCall(
        string CommandText,
        CommandType CommandType,
        object? Parameters,
        IReadOnlyList<ParameterSpec> Specs,
        int? TimeoutSeconds,
        IReadOnlyDictionary<Type, object> Mappers) {

        private static readonly IReadOnlyList<ParameterSpec> NoSpecs = [];
        private static readonly IReadOnlyDictionary<Type, object> NoMappers = new Dictionary<Type, object>();

        public static SqlCall From(string commandText, object? parameters) {
            return new SqlCall(commandText, CommandTypeResolver.Resolve(commandText), parameters, NoSpecs, null, NoMappers);
        }

        public string CommandTypeName => CommandTypeResolver.Describe(CommandType);

        /// <summary>
        /// What interceptors see as the parameters: the object the caller passed, or the input values
        /// added through a request.
        /// </summary>
        public object? DescribeParameters() {
            if (Parameters is not null || Specs.Count == 0) {
                return Parameters;
            }

            var values = new Dictionary<string, object?>();
            foreach (var spec in Specs) {
                if (spec.IsInput) {
                    values[spec.Name] = spec.Value;
                }
            }

            return values;
        }

    }
}
