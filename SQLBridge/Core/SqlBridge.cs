using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace SQLBridge {
    /// <summary>
    /// The one class applications use for SQL Server access: queries, stored procedures, transactions,
    /// bulk copy, caching, retry, interceptors and diagnostics.
    /// </summary>
    /// <example>
    /// <code>
    /// using var client = new SqlBridge("Server=...;Database=...");
    /// var policies = await client.QueryAsync&lt;Policy&gt;("usp_GET_Policy", new { PolicyId = 1 });
    /// </code>
    /// </example>
    public sealed class SqlBridge : ISqlClient, IDisposable {

        private readonly SqlClientOperations _operations;
        private readonly MemoryQueryCache _queryCache;
        private readonly SqlBridgeOptions _options;
        private readonly ILogger _logger;

        /// <summary>Creates a client with default options: 30 s timeout, 5 retries, caching on.</summary>
        /// <param name="connectionString">SQL Server connection string.</param>
        /// <exception cref="InvalidOperationException">The connection string is empty.</exception>
        public SqlBridge(string connectionString)
            : this(new SqlBridgeOptions { ConnectionString = connectionString }) {
        }

        /// <summary>Creates a client from full options.</summary>
        /// <param name="options">Settings. Interceptors listed here are used.</param>
        /// <param name="loggerFactory">Optional. Slow queries, retries and failures are logged through it.</param>
        /// <param name="interceptors">Optional extra interceptors, run after those in <paramref name="options"/>.</param>
        public SqlBridge(
            SqlBridgeOptions options,
            ILoggerFactory? loggerFactory = null,
            IEnumerable<IQueryInterceptor>? interceptors = null) {
            _options = options;
            loggerFactory ??= NullLoggerFactory.Instance;
            _logger = loggerFactory.CreateLogger<SqlBridge>();

            ConnectionPoolMonitor? poolMonitor = options.EnablePoolMonitoring
                ? new ConnectionPoolMonitor(options.ConnectionString)
                : null;
            Pool = poolMonitor;

            _queryCache = new MemoryQueryCache(options.Caching.MaxEntries, options.Caching.EvictionScanInterval);

            var connectionFactory = new SqlConnectionFactory(options.ConnectionString, poolMonitor);
            var retryPolicy = new RetryPolicy(Options.Create(options), loggerFactory.CreateLogger<RetryPolicy>());
            var pipeline = new InterceptorPipeline(
                options.Interceptors.Concat(interceptors ?? []),
                loggerFactory.CreateLogger<InterceptorPipeline>());

            _operations = new SqlClientOperations(connectionFactory, retryPolicy, options, _logger, pipeline);
        }

        /// <inheritdoc />
        public IQueryCache Cache => _queryCache;

        /// <inheritdoc />
        public IConnectionPoolMonitor? Pool { get; }

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
        public async Task<StoredProcedureResult<T>> ExecuteWithOutputAsync<T>(string procedureName,
            object? parameters = null, Action<IStoredProcedureBuilder>? configure = null,
            CancellationToken ct = default) where T : new() {
            return await _operations.ExecuteWithOutputAsync<T>(procedureName, parameters, configure, ct);
        }

        /// <inheritdoc />
        public IStoredProcedureBuilder StoredProcedure(string procedureName) {
            return _operations.StoredProcedure(procedureName);
        }

        /// <inheritdoc />
        public IBulkCopyBuilder BulkCopy(string destinationTable) {
            return _operations.BulkCopy(destinationTable);
        }

        // === Cached queries ===

        /// <inheritdoc />
        public async Task<IReadOnlyList<T>> QueryCachedAsync<T>(string commandText, object? parameters = null,
            TimeSpan? ttl = null, CancellationToken ct = default) where T : new() {
            if (!_options.Caching.Enabled) {
                return await _operations.QueryAsync<T>(commandText, parameters, ct);
            }

            var cacheKey = CacheKeyBuilder.Build(commandText, parameters);

            if (_queryCache.TryGet<T>(cacheKey, out var cached)) {
                _logger.LogDebug("Cache HIT: {CommandText} -> {Key}", commandText, cacheKey);
                return cached;
            }

            _logger.LogDebug("Cache MISS: {CommandText} -> {Key}", commandText, cacheKey);

            var result = await _operations.QueryAsync<T>(commandText, parameters, ct);
            _queryCache.Set(cacheKey, result, ttl ?? _options.Caching.DefaultTtl);
            return result;
        }

        /// <inheritdoc />
        public void InvalidateCache(string commandText) {
            _queryCache.InvalidateByPrefix(commandText);
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

        /// <inheritdoc />
        public void Dispose() {
            _queryCache.Dispose();
        }

    }
}
