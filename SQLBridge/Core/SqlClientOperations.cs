using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SQLBridge {
    /// <summary>
    /// Operations against the database for the client: a pooled connection per call, with retry,
    /// interceptors, logging and diagnostics around each run.
    /// </summary>
    internal sealed class SqlClientOperations : SqlOperations {

        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly RetryPolicy _retryPolicy;
        private readonly ILogger _logger;
        private readonly InterceptorPipeline _interceptorPipeline;
        private readonly MemoryQueryCache _queryCache;

        public SqlClientOperations(
            ISqlConnectionFactory connectionFactory,
            RetryPolicy retryPolicy,
            SqlBridgeOptions options,
            ILogger logger,
            InterceptorPipeline interceptorPipeline,
            MemoryQueryCache queryCache) : base(options) {
            _queryCache = queryCache;
            _connectionFactory = connectionFactory;
            _retryPolicy = retryPolicy;
            _logger = logger;
            _interceptorPipeline = interceptorPipeline;
        }

        public async Task<ISqlTransaction> BeginTransactionAsync(CancellationToken ct) {
            var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
            return new SqlTransactionWrapper(connection, transaction, _retryPolicy, Options);
        }

        public async Task PingAsync(CancellationToken ct) {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 5;
            await command.ExecuteScalarAsync(ct);
        }

        public override IBulkCopyBuilder BulkCopy(string destinationTable) {
            return new BulkCopyBuilder(_connectionFactory, _retryPolicy, destinationTable);
        }

        internal override async Task<IReadOnlyList<T>> RunCachedQueryAsync<T>(SqlCall call, TimeSpan? ttl,
            CancellationToken ct) {
            if (!Options.Caching.Enabled) {
                return await RunQueryAsync<T>(call, ct);
            }

            var cacheKey = CacheKeyBuilder.Build(call, typeof(T));

            if (_queryCache.TryGet<T>(cacheKey, out var cached)) {
                _logger.LogDebug("Cache HIT: {CommandText} -> {Key}", call.CommandText, cacheKey);
                return cached;
            }

            _logger.LogDebug("Cache MISS: {CommandText} -> {Key}", call.CommandText, cacheKey);

            var result = await RunQueryAsync<T>(call, ct);
            _queryCache.Set(cacheKey, result, ttl ?? Options.Caching.DefaultTtl);
            return result;
        }

        protected override async Task<CommandScope> OpenAsync(SqlCall call, CancellationToken ct) {
            var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);
            var command = CreateCommand(connection, null, call);
            return new CommandScope(command, connection);
        }

        protected override async Task<T> RunAsync<T>(SqlCall call,
            Func<SqlCall, CancellationToken, Task<T>> operation, CancellationToken ct) {
            // Interceptors see the command text and parameters before the real command runs
            if (_interceptorPipeline.HasInterceptors) {
                return await RunWithInterceptorsAsync(call, operation, ct);
            }

            var stopwatch = Stopwatch.StartNew();
            try {
                var result = await _retryPolicy.ExecuteAsync(call, operation, ct);
                stopwatch.Stop();

                LogQueryCompleted(call.CommandText, call.CommandTypeName, stopwatch.Elapsed);
                RaiseDiagnostics(call.CommandText, call.CommandTypeName, stopwatch.Elapsed, succeeded: true);

                return result;
            } catch (Exception ex) {
                stopwatch.Stop();
                LogFailure(call, ex, stopwatch.Elapsed);
                throw;
            }
        }

        private async Task<T> RunWithInterceptorsAsync<T>(SqlCall call,
            Func<SqlCall, CancellationToken, Task<T>> operation, CancellationToken ct) {
            var stopwatch = Stopwatch.StartNew();

            try {
                var result = await _interceptorPipeline.ExecuteAsync(
                    new PendingRun<T>(call, operation),
                    call,
                    RetryAsync,
                    ct);

                stopwatch.Stop();
                LogQueryCompleted(call.CommandText, call.CommandTypeName, stopwatch.Elapsed);
                RaiseDiagnostics(call.CommandText, call.CommandTypeName, stopwatch.Elapsed, succeeded: true);

                return result;
            } catch (Exception ex) {
                stopwatch.Stop();
                LogFailure(call, ex, stopwatch.Elapsed);
                throw;
            }
        }

        private async Task<T> RetryAsync<T>(PendingRun<T> run, CancellationToken token) {
            return await _retryPolicy.ExecuteAsync(run.Call, run.Operation, token);
        }

        private void LogFailure(SqlCall call, Exception exception, TimeSpan elapsed) {
            _logger.LogError(exception, "Query failed: {CommandText} ({CommandType}) after {ElapsedMs}ms",
                call.CommandText, call.CommandTypeName, elapsed.TotalMilliseconds);

            RaiseDiagnostics(call.CommandText, call.CommandTypeName, elapsed, succeeded: false, error: exception.Message);
        }

        private void LogQueryCompleted(string commandText, string commandType, TimeSpan elapsed) {
            if (elapsed > Options.SlowQueryThreshold) {
                _logger.LogWarning(
                    "SLOW QUERY: {CommandText} ({CommandType}) took {ElapsedMs}ms (threshold: {ThresholdMs}ms)",
                    commandText, commandType, elapsed.TotalMilliseconds,
                    Options.SlowQueryThreshold.TotalMilliseconds);
            } else if (_logger.IsEnabled(LogLevel.Debug)) {
                _logger.LogDebug(
                    "Query completed: {CommandText} ({CommandType}) in {ElapsedMs}ms",
                    commandText, commandType, elapsed.TotalMilliseconds);
            }
        }

        private void RaiseDiagnostics(string commandText, string commandType, TimeSpan elapsed,
            bool succeeded, string? error = null) {
            Options.OnQueryExecuted?.Invoke(new QueryDiagnostics {
                CommandText = commandText,
                CommandType = commandType,
                Duration = elapsed,
                RowsAffected = -1,
                Succeeded = succeeded,
                ErrorMessage = error
            });
        }

    }
}
