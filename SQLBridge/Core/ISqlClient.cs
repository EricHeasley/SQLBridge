namespace SQLBridge {
    /// <summary>
    /// The single entry point for SQLBridge. Create one with <c>new SqlBridge(connectionString)</c>
    /// or inject it after <c>services.AddSqlBridge(...)</c>.
    /// </summary>
    public interface ISqlClient : ISqlOperations {

        // === Cached queries ===

        /// <summary>
        /// Runs a query with result caching. Results are cached by command text + parameters.
        /// </summary>
        Task<IReadOnlyList<T>> QueryCachedAsync<T>(string commandText, object? parameters = null,
            TimeSpan? ttl = null, CancellationToken ct = default) where T : new();

        /// <summary>
        /// Invalidates cached results for a command text (all parameter combinations).
        /// </summary>
        void InvalidateCache(string commandText);

        /// <summary>
        /// Query cache for stats and clearing.
        /// </summary>
        IQueryCache Cache { get; }

        /// <summary>
        /// Connection pool monitor. Null unless <see cref="SqlBridgeOptions.EnablePoolMonitoring"/> is true.
        /// </summary>
        IConnectionPoolMonitor? Pool { get; }

        // === Transactions ===

        /// <summary>
        /// Begins a transaction. The returned object has the same operations as the client.
        /// </summary>
        Task<ISqlTransaction> BeginTransactionAsync(CancellationToken ct = default);

        // === Health ===

        /// <summary>
        /// Opens a connection and runs SELECT 1. Throws if the database is unreachable.
        /// </summary>
        Task PingAsync(CancellationToken ct = default);

    }
}
