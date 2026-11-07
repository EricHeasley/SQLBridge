namespace SQLBridge {
    /// <summary>
    /// Outcome of a stored procedure: the rows it returned plus its output parameters.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    public sealed class StoredProcedureResult<T> {

        /// <summary>The mapped rows of the first result set.</summary>
        public IReadOnlyList<T> Data { get; init; } = [];

        /// <summary>Rows affected as reported by SQL Server.</summary>
        public int RowsAffected { get; init; }

        /// <summary>Output, input/output and return value parameters.</summary>
        public OutputParameterCollection OutputParams { get; init; } = new();

    }
}
