using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace SQLBridge {
    /// <summary>
    /// The one class applications use for SQL Server access: queries, stored procedures, parameters,
    /// result mapping, caching, transactions, bulk copy, retry, interceptors and diagnostics.
    /// </summary>
    /// <example>
    /// <code>
    /// using var client = new SqlBridgeClient("Server=...;Database=...");
    ///
    /// var policies = await client.QueryAsync&lt;Policy&gt;("usp_GET_Policy", new { PolicyId = 1 });
    ///
    /// var cached = await client.Command("usp_GET_Policy")
    ///     .WithParam("Region", "TX")
    ///     .CacheFor(TimeSpan.FromMinutes(5))
    ///     .QueryAsync&lt;Policy&gt;();
    /// </code>
    /// </example>
    public sealed class SqlBridgeClient : ISqlClient, IDisposable {

        private readonly SqlClientOperations _operations;
        private readonly MemoryQueryCache _queryCache;
        private readonly ConnectionPoolMonitor? _poolMonitor;
        private readonly SqlBridgeOptions _options;

        /// <summary>Creates a client with default options: 30 s timeout, 5 retries, caching on.</summary>
        /// <param name="connectionString">SQL Server connection string.</param>
        /// <exception cref="InvalidOperationException">The connection string is empty.</exception>
        public SqlBridgeClient(string connectionString)
            : this(new SqlBridgeOptions { ConnectionString = connectionString }) {
        }

        /// <summary>Creates a client from full options.</summary>
        /// <param name="options">Settings. Interceptors and mappers listed here are used.</param>
        /// <param name="loggerFactory">Optional. Slow queries, retries and failures are logged through it.</param>
        /// <param name="interceptors">Optional extra interceptors, run after those in <paramref name="options"/>.</param>
        public SqlBridgeClient(
            SqlBridgeOptions options,
            ILoggerFactory? loggerFactory = null,
            IEnumerable<IQueryInterceptor>? interceptors = null) {
            _options = options;
            loggerFactory ??= NullLoggerFactory.Instance;

            _poolMonitor = options.EnablePoolMonitoring ? new ConnectionPoolMonitor() : null;
            _queryCache = new MemoryQueryCache(options.Caching.MaxEntries, options.Caching.EvictionScanInterval);

            var connectionFactory = new SqlConnectionFactory(options.ConnectionString, _poolMonitor);
            var retryPolicy = new RetryPolicy(Options.Create(options), loggerFactory.CreateLogger<RetryPolicy>());
            var pipeline = new InterceptorPipeline(
                options.Interceptors.Concat(interceptors ?? []),
                loggerFactory.CreateLogger<InterceptorPipeline>());

            _operations = new SqlClientOperations(
                connectionFactory, retryPolicy, options, loggerFactory.CreateLogger<SqlBridgeClient>(), pipeline, _queryCache);
        }

        // === Queries and commands ===

        /// <inheritdoc />
        public async Task<IReadOnlyList<T>> QueryAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : new() {
            return await _operations.QueryAsync<T>(commandText, parameters, ct);
        }

        /// <inheritdoc />
        public async Task<T?> QuerySingleAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : class, new() {
            return await _operations.QuerySingleAsync<T>(commandText, parameters, ct);
        }

        /// <inheritdoc />
        public async Task<int> ExecuteAsync(string commandText, object? parameters = null,
            CancellationToken ct = default) {
            return await _operations.ExecuteAsync(commandText, parameters, ct);
        }

        /// <inheritdoc />
        public async Task<object?> ScalarAsync(string commandText, object? parameters = null,
            CancellationToken ct = default) {
            return await _operations.ScalarAsync(commandText, parameters, ct);
        }

        /// <inheritdoc />
        public IAsyncEnumerable<T> StreamAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : new() {
            return _operations.StreamAsync<T>(commandText, parameters, ct);
        }

        /// <inheritdoc />
        public ISqlRequest Command(string commandText) {
            return _operations.Command(commandText);
        }

        /// <inheritdoc />
        public IBulkCopyBuilder BulkCopy(string destinationTable) {
            return _operations.BulkCopy(destinationTable);
        }

        // === Cache ===

        /// <inheritdoc />
        public void InvalidateCache(string commandText) {
            _queryCache.InvalidateByPrefix(CacheKeyBuilder.InvalidationPrefix(commandText));
        }

        /// <inheritdoc />
        public void ClearCache() {
            _queryCache.Clear();
        }

        /// <inheritdoc />
        public CacheStats GetCacheStats() {
            return _queryCache.GetStats();
        }

        // === Connections ===

        /// <inheritdoc />
        public ConnectionPoolStats? GetPoolStats() {
            return _poolMonitor?.GetStats();
        }

        /// <inheritdoc />
        public void ClearPool() {
            using var connection = new SqlConnection(_options.ConnectionString);
            SqlConnection.ClearPool(connection);
        }

        // === Transactions and health ===

        /// <inheritdoc />
        public async Task<ISqlTransaction> BeginTransactionAsync(CancellationToken ct = default) {
            return await _operations.BeginTransactionAsync(ct);
        }

        /// <inheritdoc />
        public async Task PingAsync(CancellationToken ct = default) {
            await _operations.PingAsync(ct);
        }

        /// <summary>Releases the cache's background timer.</summary>
        public void Dispose() {
            _queryCache.Dispose();
        }

    }
}
