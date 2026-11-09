namespace SQLBridge {
    /// <summary>
    /// The single entry point for SQLBridge. Create a <see cref="SqlBridgeClient"/> with
    /// <c>new SqlBridgeClient(connectionString)</c> or inject this interface after <c>services.AddSqlBridge(...)</c>.
    /// Parameters, mapping and caching are configured per call with <see cref="ISqlOperations.Command"/>.
    /// </summary>
    public interface ISqlClient : ISqlOperations {

        // === Cache ===

        /// <summary>
        /// Removes cached results for a command text (all parameter combinations).
        /// </summary>
        void InvalidateCache(string commandText);

        /// <summary>Removes every cached result.</summary>
        void ClearCache();

        /// <summary>Hits, misses, evictions and size of the query cache.</summary>
        CacheStats GetCacheStats();

        // === Connections ===

        /// <summary>
        /// Counters for connections opened through this client, or null unless
        /// <see cref="SqlBridgeOptions.EnablePoolMonitoring"/> is true.
        /// </summary>
        ConnectionPoolStats? GetPoolStats();

        /// <summary>
        /// Clears the ADO.NET connection pool, forcing connections to be re-established.
        /// Typically only needed after a failover or credential rotation.
        /// </summary>
        void ClearPool();

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
