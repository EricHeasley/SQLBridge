using System.Data;

namespace SQLBridge {
    /// <summary>
    /// Fluent builder for a stored procedure call. Get one from <see cref="ISqlOperations.StoredProcedure"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// var result = await client.StoredProcedure("usp_INSERT_Policy")
    ///     .WithParam("HolderName", "Acme", SqlDbType.NVarChar, 100)
    ///     .WithOutputParam("PolicyId", SqlDbType.Int)
    ///     .ExecuteAsync();
    /// var id = result.OutputParams.Get&lt;int&gt;("PolicyId");
    /// </code>
    /// </example>
    public interface IStoredProcedureBuilder {

        /// <summary>Adds an input parameter. The "@" prefix is optional. Null is sent as <c>DBNull</c>.</summary>
        IStoredProcedureBuilder WithParam(string name, object? value);

        /// <summary>Adds an input parameter with an explicit SQL type.</summary>
        IStoredProcedureBuilder WithParam(string name, object? value, SqlDbType dbType);

        /// <summary>Adds an input parameter with an explicit SQL type and size.</summary>
        IStoredProcedureBuilder WithParam(string name, object? value, SqlDbType dbType, int size);

        /// <summary>Adds an input/output parameter. Read it back from <see cref="StoredProcedureResult.OutputParams"/>.</summary>
        IStoredProcedureBuilder WithInputOutput(string name, object? value, SqlDbType dbType, int size = 0);

        /// <summary>Adds an output parameter. Read it back from <see cref="StoredProcedureResult.OutputParams"/>.</summary>
        IStoredProcedureBuilder WithOutputParam(string name, SqlDbType dbType, int size = 0);

        /// <summary>Captures the procedure's integer return value as <c>OutputParams.ReturnValue</c>.</summary>
        IStoredProcedureBuilder WithReturnValue();

        /// <summary>Overrides the command timeout, in seconds, for this call.</summary>
        IStoredProcedureBuilder WithTimeout(int seconds);

        /// <summary>Uses a custom mapper for <typeparamref name="T"/> instead of name-based mapping.</summary>
        IStoredProcedureBuilder WithMapper<T>(IResultMapper<T> mapper) where T : new();

        /// <summary>Runs the procedure and maps the first result set.</summary>
        Task<IReadOnlyList<T>> QueryAsync<T>(CancellationToken ct = default) where T : new();

        /// <summary>Runs the procedure and maps the first row, or returns null when there are none.</summary>
        Task<T?> QuerySingleAsync<T>(CancellationToken ct = default) where T : class, new();

        /// <summary>Runs the procedure and maps two result sets, in order.</summary>
        Task<(IReadOnlyList<T1>, IReadOnlyList<T2>)> QueryMultipleAsync<T1, T2>(
            CancellationToken ct = default) where T1 : new() where T2 : new();

        /// <summary>Runs the procedure and maps three result sets, in order.</summary>
        Task<(IReadOnlyList<T1>, IReadOnlyList<T2>, IReadOnlyList<T3>)> QueryMultipleAsync<T1, T2, T3>(
            CancellationToken ct = default) where T1 : new() where T2 : new() where T3 : new();

        /// <summary>Runs a procedure that returns no rows and captures its output parameters.</summary>
        Task<StoredProcedureResult> ExecuteAsync(CancellationToken ct = default);

        /// <summary>Runs the procedure, maps the first result set and captures output parameters.</summary>
        Task<StoredProcedureResult<T>> ExecuteAsync<T>(CancellationToken ct = default) where T : new();

        /// <summary>Runs the procedure and returns the first column of the first row, or null.</summary>
        Task<object?> ScalarAsync(CancellationToken ct = default);

        /// <summary>Runs the procedure and yields rows as they are read. Not retried.</summary>
        IAsyncEnumerable<T> StreamAsync<T>(CancellationToken ct = default) where T : new();

    }
}
