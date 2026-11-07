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

        public SqlClientOperations(
            ISqlConnectionFactory connectionFactory,
            RetryPolicy retryPolicy,
            SqlBridgeOptions options,
            ILogger logger,
            InterceptorPipeline interceptorPipeline) : base(options) {
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

        public override IStoredProcedureBuilder StoredProcedure(string procedureName) {
            return new StoredProcedureBuilder(_connectionFactory, _retryPolicy, Options, procedureName);
        }

        public override IBulkCopyBuilder BulkCopy(string destinationTable) {
            return new BulkCopyBuilder(_connectionFactory, _retryPolicy, destinationTable);
        }

        protected override async Task<CommandScope> OpenAsync(string commandText, CommandType commandType,
            object? parameters, CancellationToken ct) {
            var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);
            var command = CreateCommand(connection, null, commandText, commandType, parameters);
            return new CommandScope(command, connection);
        }

        protected override async Task<T> RunAsync<T>(string commandText, CommandType commandType,
            Func<CancellationToken, Task<T>> action, CancellationToken ct) {
            var commandTypeName = CommandTypeResolver.Describe(commandType);

            // Interceptors get a command to inspect before the real one runs
            if (_interceptorPipeline.HasInterceptors) {
                return await RunWithInterceptorsAsync(commandText, commandType, commandTypeName, action, ct);
            }

            var stopwatch = Stopwatch.StartNew();
            try {
                var result = await _retryPolicy.ExecuteAsync(action, ct);
                stopwatch.Stop();

                LogQueryCompleted(commandText, commandTypeName, stopwatch.Elapsed);
                RaiseDiagnostics(commandText, commandTypeName, stopwatch.Elapsed, succeeded: true);

                return result;
            } catch (Exception ex) {
                stopwatch.Stop();
                _logger.LogError(ex, "Query failed: {CommandText} ({CommandType}) after {ElapsedMs}ms",
                    commandText, commandTypeName, stopwatch.ElapsedMilliseconds);

                RaiseDiagnostics(commandText, commandTypeName, stopwatch.Elapsed, succeeded: false, error: ex.Message);
                throw;
            }
        }

        private async Task<T> RunWithInterceptorsAsync<T>(
            string commandText, CommandType commandType, string commandTypeName,
            Func<CancellationToken, Task<T>> action, CancellationToken ct) {
            var stopwatch = Stopwatch.StartNew();

            // Not attached to a connection, so an interceptor can short-circuit without touching the database
            await using var command = new SqlCommand(commandText) {
                CommandType = commandType,
                CommandTimeout = Options.DefaultCommandTimeout
            };

            try {
                var result = await _interceptorPipeline.ExecuteAsync(
                    command,
                    commandText,
                    commandTypeName,
                    async (cmd, token) => await _retryPolicy.ExecuteAsync(action, token),
                    ct);

                stopwatch.Stop();
                LogQueryCompleted(commandText, commandTypeName, stopwatch.Elapsed);
                RaiseDiagnostics(commandText, commandTypeName, stopwatch.Elapsed, succeeded: true);

                return result;
            } catch (Exception ex) {
                stopwatch.Stop();
                _logger.LogError(ex, "Query failed: {CommandText} ({CommandType}) after {ElapsedMs}ms",
                    commandText, commandTypeName, stopwatch.ElapsedMilliseconds);

                RaiseDiagnostics(commandText, commandTypeName, stopwatch.Elapsed, succeeded: false, error: ex.Message);
                throw;
            }
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
