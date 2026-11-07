namespace SQLBridge {
    /// <summary>
    /// Outcome of a stored procedure that returns no rows.
    /// </summary>
    public sealed class StoredProcedureResult {

        /// <summary>Rows affected as reported by SQL Server.</summary>
        public int RowsAffected { get; init; }

        /// <summary>Output, input/output and return value parameters.</summary>
        public OutputParameterCollection OutputParams { get; init; } = new();

    }
}
