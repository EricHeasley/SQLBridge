using System.Data;

namespace SQLBridge {
    /// <summary>
    /// One command being built: parameters, mapping and caching are all steps on the same request,
    /// finished by a terminal call such as <see cref="QueryAsync{T}"/>. Start one with
    /// <see cref="ISqlOperations.Command"/>.
    /// </summary>
    /// <remarks>
    /// Works for stored procedures and SQL text alike (see <see cref="ISqlOperations"/>). Retry,
    /// interceptors, logging and diagnostics apply exactly as for the simple methods.
    /// </remarks>
    /// <example>
    /// <code>
    /// var policies = await client.Command("usp_GET_Policy")
    ///     .WithParam("Region", "TX")
    ///     .MapWith(new PolicyMapper())
    ///     .CacheFor(TimeSpan.FromMinutes(5))
    ///     .QueryAsync&lt;Policy&gt;();
    ///
    /// var result = await client.Command("usp_INSERT_Policy")
    ///     .WithParam("HolderName", "Acme", SqlDbType.NVarChar, 100)
    ///     .WithOutputParam("PolicyId", SqlDbType.Int)
    ///     .ExecuteAsync();
    /// var id = result.OutputParams.Get&lt;int&gt;("PolicyId");
    /// </code>
    /// </example>
    public interface ISqlRequest {

        // === Parameters ===

        /// <summary>Adds an input parameter. The "@" prefix is optional. Null is sent as <c>DBNull</c>.</summary>
        ISqlRequest WithParam(string name, object? value);

        /// <summary>Adds an input parameter with an explicit SQL type.</summary>
        ISqlRequest WithParam(string name, object? value, SqlDbType dbType);

        /// <summary>Adds an input parameter with an explicit SQL type and size.</summary>
        ISqlRequest WithParam(string name, object? value, SqlDbType dbType, int size);

        /// <summary>
        /// Adds several input parameters at once from an anonymous object, a POCO, a
        /// <c>Dictionary&lt;string, object?&gt;</c> or <c>SqlParameter[]</c>.
        /// </summary>
        ISqlRequest WithParams(object? parameters);

        /// <summary>Adds an input/output parameter. Read it back from <see cref="CommandResult.OutputParams"/>.</summary>
        ISqlRequest WithInputOutput(string name, object? value, SqlDbType dbType, int size = 0);

        /// <summary>Adds an output parameter. Read it back from <see cref="CommandResult.OutputParams"/>.</summary>
        ISqlRequest WithOutputParam(string name, SqlDbType dbType, int size = 0);

        /// <summary>Captures the procedure's integer return value as <c>OutputParams.ReturnValue</c>.</summary>
        ISqlRequest WithReturnValue();

        /// <summary>Overrides the command timeout, in seconds, for this request.</summary>
        ISqlRequest WithTimeout(int seconds);

        // === Mapping and caching ===

        /// <summary>
        /// Uses a custom mapper for <typeparamref name="T"/> in this request instead of name-based mapping.
        /// A mapper registered with <see cref="SqlBridgeOptions.AddMapper{T}"/> applies to every request.
        /// </summary>
        ISqlRequest MapWith<T>(IResultMapper<T> mapper) where T : new();

        /// <summary>
        /// Caches the result of <see cref="QueryAsync{T}"/> by command text, parameter values and row type.
        /// Only <see cref="QueryAsync{T}"/> supports caching; other terminal calls throw
        /// <see cref="InvalidOperationException"/>, and so does a request made inside a transaction.
        /// </summary>
        /// <param name="ttl">How long the result stays valid. Null uses <see cref="CacheOptions.DefaultTtl"/>.</param>
        ISqlRequest CacheFor(TimeSpan? ttl = null);

        // === Run it ===

        /// <summary>Runs the command and maps every row of the first result set.</summary>
        Task<IReadOnlyList<T>> QueryAsync<T>(CancellationToken ct = default) where T : new();

        /// <summary>Runs the command and maps the first row, or returns null when there are none.</summary>
        Task<T?> QuerySingleAsync<T>(CancellationToken ct = default) where T : class, new();

        /// <summary>Runs the command and maps two result sets, in order.</summary>
        Task<(IReadOnlyList<T1>, IReadOnlyList<T2>)> QueryMultipleAsync<T1, T2>(
            CancellationToken ct = default) where T1 : new() where T2 : new();

        /// <summary>Runs the command and maps three result sets, in order.</summary>
        Task<(IReadOnlyList<T1>, IReadOnlyList<T2>, IReadOnlyList<T3>)> QueryMultipleAsync<T1, T2, T3>(
            CancellationToken ct = default) where T1 : new() where T2 : new() where T3 : new();

        /// <summary>Runs a command that returns no rows and captures its output parameters.</summary>
        Task<CommandResult> ExecuteAsync(CancellationToken ct = default);

        /// <summary>Runs the command, maps the first result set and captures output parameters.</summary>
        Task<CommandResult<T>> ExecuteAsync<T>(CancellationToken ct = default) where T : new();

        /// <summary>Runs the command and returns the first column of the first row, or null.</summary>
        Task<object?> ScalarAsync(CancellationToken ct = default);

        /// <summary>Runs the command and yields rows as they are read. Not retried.</summary>
        IAsyncEnumerable<T> StreamAsync<T>(CancellationToken ct = default) where T : new();

    }
}
