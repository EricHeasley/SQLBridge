using SQLBridge.StoredProcedures;
using SQLBridge.BulkOperations;
using SQLBridge.Caching;
using SQLBridge.Pool;
using SQLBridge.Transactions;

namespace SQLBridge.Core {
    public interface ISqlClient {

        // === Stored Procedure: Simple API ===
        Task<IReadOnlyList<T>> ExecAsync<T>(string procedureName, object? parameters = null,
            CancellationToken ct = default) where T : new();

        Task<T?> ExecSingleAsync<T>(string procedureName, object? parameters = null,
            CancellationToken ct = default) where T : class, new();

        Task<SprocResult<T>> ExecWithOutputAsync<T>(string procedureName, object? parameters = null,
            Action<IStoredProcedureBuilder>? configure = null,
            CancellationToken ct = default) where T : new();

        Task<int> ExecNonQueryAsync(string procedureName, object? parameters = null,
            CancellationToken ct = default);

        Task<object?> ExecScalarAsync(string procedureName, object? parameters = null,
            CancellationToken ct = default);

        // === Stored Procedure: Streaming ===
        IAsyncEnumerable<T> ExecStreamAsync<T>(string procedureName, object? parameters = null,
            CancellationToken ct = default) where T : new();

        // === Stored Procedure: Fluent API ===
        IStoredProcedureBuilder StoredProc(string procedureName);

        // === Raw SQL ===
        Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null,
            CancellationToken ct = default) where T : new();

        Task<T?> QuerySingleAsync<T>(string sql, object? parameters = null,
            CancellationToken ct = default) where T : class, new();

        Task<int> ExecuteAsync(string sql, object? parameters = null,
            CancellationToken ct = default);

        Task<object?> ScalarAsync(string sql, object? parameters = null,
            CancellationToken ct = default);

        // === Raw SQL: Streaming ===
        IAsyncEnumerable<T> StreamAsync<T>(string sql, object? parameters = null,
            CancellationToken ct = default) where T : new();

        // === Cached Queries ===

        /// <summary>
        /// Executes a stored procedure with result caching. Results are cached by procedure name + parameters.
        /// </summary>
        Task<IReadOnlyList<T>> ExecCachedAsync<T>(string procedureName, object? parameters = null,
            TimeSpan? ttl = null, CancellationToken ct = default) where T : new();

        /// <summary>
        /// Executes a raw SQL query with result caching.
        /// </summary>
        Task<IReadOnlyList<T>> QueryCachedAsync<T>(string sql, object? parameters = null,
            TimeSpan? ttl = null, CancellationToken ct = default) where T : new();

        /// <summary>
        /// Invalidates cached results for a specific stored procedure (all parameter combinations).
        /// </summary>
        void InvalidateCache(string procedureName);

        /// <summary>
        /// Gets the query cache instance for advanced operations (stats, clear, etc.).
        /// Returns null if caching is not configured.
        /// </summary>
        IQueryCache? Cache { get; }

        /// <summary>
        /// Gets the connection pool monitor for observing pool health.
        /// Returns null if pool monitoring is not configured.
        /// </summary>
        IConnectionPoolMonitor? Pool { get; }

        // === Transactions ===
        Task<ISqlTransaction> BeginTransactionAsync(CancellationToken ct = default);

        // === Bulk Operations ===
        IBulkCopyBuilder BulkCopy(string destinationTable);

    }
}
