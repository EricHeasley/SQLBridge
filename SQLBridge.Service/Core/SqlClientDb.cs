using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SQLBridge.Model.Caching;
using SQLBridge.Model.Configuration;
using SQLBridge.Model.Diagnostics;
using SQLBridge.Model.StoredProcedures;
using SQLBridge.Service.BulkOperations;
using SQLBridge.Service.Caching;
using SQLBridge.Service.Contracts.BulkOperations;
using SQLBridge.Service.Contracts.Caching;
using SQLBridge.Service.Contracts.Core;
using SQLBridge.Service.Contracts.Pool;
using SQLBridge.Service.Contracts.StoredProcedures;
using SQLBridge.Service.Contracts.Transactions;
using SQLBridge.Service.Interceptors;
using SQLBridge.Service.Mapping;
using SQLBridge.Service.Resilience;
using SQLBridge.Service.StoredProcedures;
using SQLBridge.Service.Transactions;

namespace SQLBridge.Service.Core {
    internal sealed class SqlClientDb : ISqlClient {

        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly RetryPolicy _retryPolicy;
        private readonly SqlClientOptions _options;
        private readonly ILogger<SqlClientDb> _logger;
        private readonly IQueryCache? _queryCache;
        private readonly CacheOptions? _cacheOptions;
        private readonly IConnectionPoolMonitor? _poolMonitor;
        private readonly InterceptorPipeline? _interceptorPipeline;

        public SqlClientDb(
            ISqlConnectionFactory connectionFactory,
            RetryPolicy retryPolicy,
            IOptions<SqlClientOptions> options,
            ILogger<SqlClientDb> logger,
            IQueryCache? queryCache = null,
            IOptions<CacheOptions>? cacheOptions = null,
            IConnectionPoolMonitor? poolMonitor = null,
            InterceptorPipeline? interceptorPipeline = null) {
            _connectionFactory = connectionFactory;
            _retryPolicy = retryPolicy;
            _options = options.Value;
            _logger = logger;
            _queryCache = queryCache;
            _cacheOptions = cacheOptions?.Value;
            _poolMonitor = poolMonitor;
            _interceptorPipeline = interceptorPipeline;
        }

        public IQueryCache? Cache => _queryCache;
        public IConnectionPoolMonitor? Pool => _poolMonitor;

        // === Stored Procedure: Simple API ===

        public async Task<IReadOnlyList<T>> ExecAsync<T>(string procedureName, object? parameters = null,
            CancellationToken ct = default) where T : new() {
            return await ExecuteWithDiagnostics(procedureName, "StoredProcedure", async token => {
                await using var connection = await _connectionFactory.CreateOpenConnectionAsync(token);
                await using var command = CreateSprocCommand(connection, procedureName, parameters);

                var mapper = new ReflectionResultMapper<T>();
                var results = new List<T>();

                await using var reader = await command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) {
                    results.Add(mapper.Map(reader));
                }

                return (IReadOnlyList<T>)results;
            }, ct);
        }

        public async Task<T?> ExecSingleAsync<T>(string procedureName, object? parameters = null,
            CancellationToken ct = default) where T : class, new() {
            return await ExecuteWithDiagnostics(procedureName, "StoredProcedure", async token => {
                await using var connection = await _connectionFactory.CreateOpenConnectionAsync(token);
                await using var command = CreateSprocCommand(connection, procedureName, parameters);

                var mapper = new ReflectionResultMapper<T>();

                await using var reader = await command.ExecuteReaderAsync(token);
                return await reader.ReadAsync(token) ? mapper.Map(reader) : null;
            }, ct);
        }

        public async Task<SprocResult<T>> ExecWithOutputAsync<T>(string procedureName, object? parameters = null,
            Action<IStoredProcedureBuilder>? configure = null,
            CancellationToken ct = default) where T : new() {
            var builder = StoredProc(procedureName);

            if (parameters is not null) {
                var sqlParams = ParameterBuilder.FromObject(parameters);
                foreach (var param in sqlParams) {
                    builder.WithParam(param.ParameterName, param.Value == DBNull.Value ? null : param.Value);
                }
            }

            configure?.Invoke(builder);
            return await builder.ExecuteAsync<T>(ct);
        }

        public async Task<int> ExecNonQueryAsync(string procedureName, object? parameters = null,
            CancellationToken ct = default) {
            return await ExecuteWithDiagnostics(procedureName, "StoredProcedure", async token => {
                await using var connection = await _connectionFactory.CreateOpenConnectionAsync(token);
                await using var command = CreateSprocCommand(connection, procedureName, parameters);
                return await command.ExecuteNonQueryAsync(token);
            }, ct);
        }

        public async Task<object?> ExecScalarAsync(string procedureName, object? parameters = null,
            CancellationToken ct = default) {
            return await ExecuteWithDiagnostics(procedureName, "StoredProcedure", async token => {
                await using var connection = await _connectionFactory.CreateOpenConnectionAsync(token);
                await using var command = CreateSprocCommand(connection, procedureName, parameters);
                var result = await command.ExecuteScalarAsync(token);
                return result == DBNull.Value ? null : result;
            }, ct);
        }

        // === Stored Procedure: Streaming ===

        public async IAsyncEnumerable<T> ExecStreamAsync<T>(string procedureName, object? parameters = null,
            [EnumeratorCancellation] CancellationToken ct = default) where T : new() {
            var sw = Stopwatch.StartNew();
            var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);
            await using var _ = connection;

            var command = CreateSprocCommand(connection, procedureName, parameters);
            await using var __ = command;

            var mapper = new ReflectionResultMapper<T>();
            var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            await using var ___ = reader;

            var count = 0;
            while (await reader.ReadAsync(ct)) {
                yield return mapper.Map(reader);
                count++;
            }

            sw.Stop();
            LogQueryCompleted(procedureName, "StoredProcedure/Stream", sw.Elapsed, count);
        }

        // === Stored Procedure: Fluent API ===

        public IStoredProcedureBuilder StoredProc(string procedureName) {
            return new StoredProcedureBuilder(_connectionFactory, _retryPolicy, _options, procedureName);
        }

        // === Raw SQL ===

        public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null,
            CancellationToken ct = default) where T : new() {
            return await ExecuteWithDiagnostics(sql, "Text", async token => {
                await using var connection = await _connectionFactory.CreateOpenConnectionAsync(token);
                await using var command = CreateTextCommand(connection, sql, parameters);

                var mapper = new ReflectionResultMapper<T>();
                var results = new List<T>();

                await using var reader = await command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) {
                    results.Add(mapper.Map(reader));
                }

                return (IReadOnlyList<T>)results;
            }, ct);
        }

        public async Task<T?> QuerySingleAsync<T>(string sql, object? parameters = null,
            CancellationToken ct = default) where T : class, new() {
            return await ExecuteWithDiagnostics(sql, "Text", async token => {
                await using var connection = await _connectionFactory.CreateOpenConnectionAsync(token);
                await using var command = CreateTextCommand(connection, sql, parameters);

                var mapper = new ReflectionResultMapper<T>();

                await using var reader = await command.ExecuteReaderAsync(token);
                return await reader.ReadAsync(token) ? mapper.Map(reader) : null;
            }, ct);
        }

        public async Task<int> ExecuteAsync(string sql, object? parameters = null,
            CancellationToken ct = default) {
            return await ExecuteWithDiagnostics(sql, "Text", async token => {
                await using var connection = await _connectionFactory.CreateOpenConnectionAsync(token);
                await using var command = CreateTextCommand(connection, sql, parameters);
                return await command.ExecuteNonQueryAsync(token);
            }, ct);
        }

        public async Task<object?> ScalarAsync(string sql, object? parameters = null,
            CancellationToken ct = default) {
            return await ExecuteWithDiagnostics(sql, "Text", async token => {
                await using var connection = await _connectionFactory.CreateOpenConnectionAsync(token);
                await using var command = CreateTextCommand(connection, sql, parameters);
                var result = await command.ExecuteScalarAsync(token);
                return result == DBNull.Value ? null : result;
            }, ct);
        }

        // === Raw SQL: Streaming ===

        public async IAsyncEnumerable<T> StreamAsync<T>(string sql, object? parameters = null,
            [EnumeratorCancellation] CancellationToken ct = default) where T : new() {
            var sw = Stopwatch.StartNew();
            var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);
            await using var _ = connection;

            var command = CreateTextCommand(connection, sql, parameters);
            await using var __ = command;

            var mapper = new ReflectionResultMapper<T>();
            var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            await using var ___ = reader;

            var count = 0;
            while (await reader.ReadAsync(ct)) {
                yield return mapper.Map(reader);
                count++;
            }

            sw.Stop();
            LogQueryCompleted(sql, "Text/Stream", sw.Elapsed, count);
        }

        // === Cached Queries ===

        public async Task<IReadOnlyList<T>> ExecCachedAsync<T>(string procedureName, object? parameters = null,
            TimeSpan? ttl = null, CancellationToken ct = default) where T : new() {
            if (_queryCache is null || _cacheOptions is not { Enabled: true }) {
                return await ExecAsync<T>(procedureName, parameters, ct);
            }

            var cacheKey = CacheKeyBuilder.Build(procedureName, parameters);

            if (_queryCache.TryGet<T>(cacheKey, out var cached)) {
                _logger.LogDebug("Cache HIT: {ProcedureName} -> {Key}", procedureName, cacheKey);
                return cached;
            }

            _logger.LogDebug("Cache MISS: {ProcedureName} -> {Key}", procedureName, cacheKey);

            var result = await ExecAsync<T>(procedureName, parameters, ct);
            _queryCache.Set(cacheKey, result, ttl ?? _cacheOptions.DefaultTtl);
            return result;
        }

        public async Task<IReadOnlyList<T>> QueryCachedAsync<T>(string sql, object? parameters = null,
            TimeSpan? ttl = null, CancellationToken ct = default) where T : new() {
            if (_queryCache is null || _cacheOptions is not { Enabled: true }) {
                return await QueryAsync<T>(sql, parameters, ct);
            }

            var cacheKey = CacheKeyBuilder.Build(sql, parameters);

            if (_queryCache.TryGet<T>(cacheKey, out var cached)) {
                _logger.LogDebug("Cache HIT: {Sql} -> {Key}", sql, cacheKey);
                return cached;
            }

            _logger.LogDebug("Cache MISS: {Sql} -> {Key}", sql, cacheKey);

            var result = await QueryAsync<T>(sql, parameters, ct);
            _queryCache.Set(cacheKey, result, ttl ?? _cacheOptions.DefaultTtl);
            return result;
        }

        public void InvalidateCache(string procedureName) {
            _queryCache?.InvalidateByPrefix(procedureName);
        }

        // === Transactions ===

        public async Task<ISqlTransaction> BeginTransactionAsync(CancellationToken ct = default) {
            var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
            return new SqlTransactionWrapper(connection, transaction, _retryPolicy, _options);
        }

        // === Bulk Operations ===

        public IBulkCopyBuilder BulkCopy(string destinationTable) {
            return new BulkCopyBuilder(_connectionFactory, _retryPolicy, destinationTable);
        }

        // === Diagnostics & Interceptors ===

        private async Task<T> ExecuteWithDiagnostics<T>(
            string commandText, string commandType,
            Func<CancellationToken, Task<T>> action,
            CancellationToken ct) {
            // If interceptors are registered, wrap the action so they can inspect/modify the command
            if (_interceptorPipeline is { HasInterceptors: true }) {
                return await ExecuteWithInterceptors<T>(commandText, commandType, action, ct);
            }

            var sw = Stopwatch.StartNew();
            try {
                var result = await _retryPolicy.ExecuteAsync(action, ct);
                sw.Stop();

                LogQueryCompleted(commandText, commandType, sw.Elapsed);
                RaiseDiagnostics(commandText, commandType, sw.Elapsed, succeeded: true);

                return result;
            } catch (Exception ex) {
                sw.Stop();
                _logger.LogError(ex, "Query failed: {CommandText} ({CommandType}) after {ElapsedMs}ms",
                    commandText, commandType, sw.ElapsedMilliseconds);

                RaiseDiagnostics(commandText, commandType, sw.Elapsed, succeeded: false, error: ex.Message);
                throw;
            }
        }

        private async Task<T> ExecuteWithInterceptors<T>(
            string commandText, string commandType,
            Func<CancellationToken, Task<T>> action,
            CancellationToken ct) {
            var sw = Stopwatch.StartNew();

            // Create a command for the interceptor to inspect/modify
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);
            await using var command = commandType == "StoredProcedure"
                ? CreateSprocCommand(connection, commandText, null)
                : CreateTextCommand(connection, commandText, null);

            try {
                var result = await _interceptorPipeline!.ExecuteAsync(
                    command,
                    commandText,
                    commandType,
                    async (cmd, token) => await _retryPolicy.ExecuteAsync(action, token),
                    ct);

                sw.Stop();
                LogQueryCompleted(commandText, commandType, sw.Elapsed);
                RaiseDiagnostics(commandText, commandType, sw.Elapsed, succeeded: true);

                return result;
            } catch (Exception ex) {
                sw.Stop();
                _logger.LogError(ex, "Query failed: {CommandText} ({CommandType}) after {ElapsedMs}ms",
                    commandText, commandType, sw.ElapsedMilliseconds);

                RaiseDiagnostics(commandText, commandType, sw.Elapsed, succeeded: false, error: ex.Message);
                throw;
            }
        }

        private void LogQueryCompleted(string commandText, string commandType, TimeSpan elapsed, int rows = -1) {
            if (elapsed > _options.SlowQueryThreshold) {
                _logger.LogWarning(
                    "SLOW QUERY: {CommandText} ({CommandType}) took {ElapsedMs}ms (threshold: {ThresholdMs}ms)",
                    commandText, commandType, elapsed.TotalMilliseconds,
                    _options.SlowQueryThreshold.TotalMilliseconds);
            } else if (_logger.IsEnabled(LogLevel.Debug)) {
                _logger.LogDebug(
                    "Query completed: {CommandText} ({CommandType}) in {ElapsedMs}ms, rows: {Rows}",
                    commandText, commandType, elapsed.TotalMilliseconds, rows);
            }
        }

        private void RaiseDiagnostics(string commandText, string commandType, TimeSpan elapsed,
            bool succeeded, string? error = null, int rows = -1) {
            _options.OnQueryExecuted?.Invoke(new QueryDiagnostics {
                CommandText = commandText,
                CommandType = commandType,
                Duration = elapsed,
                RowsAffected = rows,
                Succeeded = succeeded,
                ErrorMessage = error
            });
        }

        // === Helpers ===

        private SqlCommand CreateSprocCommand(SqlConnection connection, string procedureName, object? parameters) {
            var command = connection.CreateCommand();
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = _options.DefaultCommandTimeout;

            var sqlParams = ParameterBuilder.FromObject(parameters);
            foreach (var param in sqlParams) {
                command.Parameters.Add(param);
            }

            return command;
        }

        private SqlCommand CreateTextCommand(SqlConnection connection, string sql, object? parameters) {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = _options.DefaultCommandTimeout;

            var sqlParams = ParameterBuilder.FromObject(parameters);
            foreach (var param in sqlParams) {
                command.Parameters.Add(param);
            }

            return command;
        }

    }
}
