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

        public SqlBridge(string connectionString)
            : this(new SqlBridgeOptions { ConnectionString = connectionString }) {
        }

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

        public IQueryCache Cache => _queryCache;

        public IConnectionPoolMonitor? Pool { get; }

        // === Queries and commands ===

        public async Task<IReadOnlyList<T>> QueryAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : new() {
            return await _operations.QueryAsync<T>(commandText, parameters, ct);
        }

        public async Task<T?> QuerySingleAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : class, new() {
            return await _operations.QuerySingleAsync<T>(commandText, parameters, ct);
        }

        public async Task<int> ExecuteAsync(string commandText, object? parameters = null,
            CancellationToken ct = default) {
            return await _operations.ExecuteAsync(commandText, parameters, ct);
        }

        public async Task<object?> ScalarAsync(string commandText, object? parameters = null,
            CancellationToken ct = default) {
            return await _operations.ScalarAsync(commandText, parameters, ct);
        }

        public IAsyncEnumerable<T> StreamAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : new() {
            return _operations.StreamAsync<T>(commandText, parameters, ct);
        }

        public async Task<StoredProcedureResult<T>> ExecuteWithOutputAsync<T>(string procedureName,
            object? parameters = null, Action<IStoredProcedureBuilder>? configure = null,
            CancellationToken ct = default) where T : new() {
            return await _operations.ExecuteWithOutputAsync<T>(procedureName, parameters, configure, ct);
        }

        public IStoredProcedureBuilder StoredProcedure(string procedureName) {
            return _operations.StoredProcedure(procedureName);
        }

        public IBulkCopyBuilder BulkCopy(string destinationTable) {
            return _operations.BulkCopy(destinationTable);
        }

        // === Cached queries ===

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

        public void InvalidateCache(string commandText) {
            _queryCache.InvalidateByPrefix(commandText);
        }

        // === Transactions and health ===

        public async Task<ISqlTransaction> BeginTransactionAsync(CancellationToken ct = default) {
            return await _operations.BeginTransactionAsync(ct);
        }

        public async Task PingAsync(CancellationToken ct = default) {
            await _operations.PingAsync(ct);
        }

        public void Dispose() {
            _queryCache.Dispose();
        }

    }
}
