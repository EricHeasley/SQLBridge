namespace SQLBridge {
    /// <summary>
    /// The operations available on both <see cref="ISqlClient"/> and <see cref="ISqlTransaction"/>.
    /// </summary>
    /// <remarks>
    /// Every method takes a <c>commandText</c> that is either a stored procedure name (no whitespace,
    /// e.g. "usp_GET_Policy" or "dbo.usp_GET_Policy") or a SQL statement (anything containing whitespace).
    /// <c>parameters</c> can be an anonymous object, a POCO, a <c>Dictionary&lt;string, object?&gt;</c> or
    /// <c>SqlParameter[]</c>; null values are sent as <c>DBNull</c>.
    /// </remarks>
    public interface ISqlOperations {

        /// <summary>Runs the command and maps every row of the first result set to <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">A class with a parameterless constructor. Columns map to properties by name, or by <see cref="ColumnAttribute"/>.</typeparam>
        /// <param name="commandText">A stored procedure name or SQL text.</param>
        /// <param name="parameters">Command parameters, or null.</param>
        /// <param name="ct">Cancels the call.</param>
        /// <returns>The rows, or an empty list.</returns>
        Task<IReadOnlyList<T>> QueryAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : new();

        /// <summary>Runs the command and maps the first row only.</summary>
        /// <returns>The first row, or null when there are none.</returns>
        Task<T?> QuerySingleAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : class, new();

        /// <summary>Runs a command that returns no rows (INSERT, UPDATE, DELETE, procedures).</summary>
        /// <returns>The number of rows affected as reported by SQL Server.</returns>
        Task<int> ExecuteAsync(string commandText, object? parameters = null,
            CancellationToken ct = default);

        /// <summary>Runs the command and returns the first column of the first row.</summary>
        /// <returns>The value, or null for no rows or <c>DBNull</c>.</returns>
        Task<object?> ScalarAsync(string commandText, object? parameters = null,
            CancellationToken ct = default);

        /// <summary>
        /// Runs the command and yields rows as they are read, without loading the whole result set.
        /// The connection stays open until enumeration finishes or is disposed. Not retried.
        /// </summary>
        IAsyncEnumerable<T> StreamAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : new();

        /// <summary>
        /// Runs a stored procedure and returns its rows together with output parameters and the return value.
        /// </summary>
        /// <param name="procedureName">The stored procedure name.</param>
        /// <param name="parameters">Input parameters, or null.</param>
        /// <param name="configure">Adds output parameters or other settings, e.g. <c>b => b.WithOutputParam("NewId", SqlDbType.Int)</c>.</param>
        /// <param name="ct">Cancels the call.</param>
        Task<StoredProcedureResult<T>> ExecuteWithOutputAsync<T>(string procedureName, object? parameters = null,
            Action<IStoredProcedureBuilder>? configure = null,
            CancellationToken ct = default) where T : new();

        /// <summary>
        /// Starts a fluent stored procedure call for typed parameters, output parameters,
        /// custom mappers and multiple result sets.
        /// </summary>
        IStoredProcedureBuilder StoredProcedure(string procedureName);

        /// <summary>Starts a bulk copy (SqlBulkCopy) into a table.</summary>
        /// <param name="destinationTable">The destination table name.</param>
        IBulkCopyBuilder BulkCopy(string destinationTable);

    }
}
