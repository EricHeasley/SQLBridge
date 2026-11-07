using System.Data;
using Microsoft.Data.SqlClient;
using SQLBridge.Configuration;
using SQLBridge.StoredProcedures;
using SQLBridge.Core;
using SQLBridge.Mapping;
using SQLBridge.Resilience;

namespace SQLBridge.Transactions {
    internal sealed class SqlTransactionWrapper : ISqlTransaction {

        private readonly SqlConnection _connection;
        private readonly SqlTransaction _transaction;
        private readonly RetryPolicy _retryPolicy;
        private readonly SqlClientOptions _options;
        private bool _disposed;

        internal SqlTransactionWrapper(
            SqlConnection connection,
            SqlTransaction transaction,
            RetryPolicy retryPolicy,
            SqlClientOptions options) {
            _connection = connection;
            _transaction = transaction;
            _retryPolicy = retryPolicy;
            _options = options;
        }

        public async Task<IReadOnlyList<T>> ExecAsync<T>(string procedureName, object? parameters = null,
            CancellationToken ct = default) where T : new() {
            await using var command = CreateCommand(procedureName, CommandType.StoredProcedure, parameters);
            var mapper = new ReflectionResultMapper<T>();
            var results = new List<T>();

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) {
                results.Add(mapper.Map(reader));
            }

            return results;
        }

        public async Task<int> ExecNonQueryAsync(string procedureName, object? parameters = null,
            CancellationToken ct = default) {
            await using var command = CreateCommand(procedureName, CommandType.StoredProcedure, parameters);
            return await command.ExecuteNonQueryAsync(ct);
        }

        public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null,
            CancellationToken ct = default) where T : new() {
            await using var command = CreateCommand(sql, CommandType.Text, parameters);
            var mapper = new ReflectionResultMapper<T>();
            var results = new List<T>();

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) {
                results.Add(mapper.Map(reader));
            }

            return results;
        }

        public async Task<int> ExecuteAsync(string sql, object? parameters = null,
            CancellationToken ct = default) {
            await using var command = CreateCommand(sql, CommandType.Text, parameters);
            return await command.ExecuteNonQueryAsync(ct);
        }

        public IStoredProcedureBuilder StoredProc(string procedureName) {
            return new StoredProcedureBuilder(
                _connection, _transaction, _retryPolicy, _options, procedureName);
        }

        public async Task CommitAsync(CancellationToken ct = default) {
            await _transaction.CommitAsync(ct);
        }

        public async Task RollbackAsync(CancellationToken ct = default) {
            await _transaction.RollbackAsync(ct);
        }

        public async ValueTask DisposeAsync() {
            if (_disposed) return;
            _disposed = true;

            await _transaction.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private SqlCommand CreateCommand(string commandText, CommandType commandType, object? parameters) {
            var command = _connection.CreateCommand();
            command.CommandText = commandText;
            command.CommandType = commandType;
            command.CommandTimeout = _options.DefaultCommandTimeout;
            command.Transaction = _transaction;

            var sqlParams = ParameterBuilder.FromObject(parameters);
            foreach (var param in sqlParams) {
                command.Parameters.Add(param);
            }

            return command;
        }

    }
}
