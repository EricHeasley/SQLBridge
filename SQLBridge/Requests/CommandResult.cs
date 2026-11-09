namespace SQLBridge {
    /// <summary>
    /// Outcome of a command that returns no rows: rows affected plus output parameters.
    /// </summary>
    public sealed class CommandResult {

        /// <summary>Rows affected as reported by SQL Server.</summary>
        public int RowsAffected { get; init; }

        /// <summary>Output, input/output and return value parameters.</summary>
        public OutputParameterCollection OutputParams { get; init; } = new();

    }
}
