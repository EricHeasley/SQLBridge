namespace SQLBridge {
    /// <summary>
    /// The operations available on both <see cref="ISqlClient"/> and <see cref="ISqlTransaction"/>.
    /// Every method takes a command text that is either a stored procedure name (no whitespace,
    /// e.g. "usp_GET_Policy" or "dbo.usp_GET_Policy") or a SQL statement (anything containing whitespace).
    /// </summary>
    public interface ISqlOperations {

        Task<IReadOnlyList<T>> QueryAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : new();

        Task<T?> QuerySingleAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : class, new();

        Task<int> ExecuteAsync(string commandText, object? parameters = null,
            CancellationToken ct = default);

        Task<object?> ScalarAsync(string commandText, object? parameters = null,
            CancellationToken ct = default);

        IAsyncEnumerable<T> StreamAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : new();

        /// <summary>
        /// Runs a stored procedure and returns rows plus output parameters and the return value.
        /// </summary>
        Task<StoredProcedureResult<T>> ExecuteWithOutputAsync<T>(string procedureName, object? parameters = null,
            Action<IStoredProcedureBuilder>? configure = null,
            CancellationToken ct = default) where T : new();

        /// <summary>
        /// Starts a fluent stored procedure call (typed parameters, output parameters, multiple result sets).
        /// </summary>
        IStoredProcedureBuilder StoredProcedure(string procedureName);

        IBulkCopyBuilder BulkCopy(string destinationTable);

    }
}
