using System.Data;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;

namespace SQLBridge {
    /// <summary>
    /// Shared implementation of <see cref="ISqlOperations"/>. The client and a transaction differ only in
    /// where commands come from and how a run is wrapped (retry, interceptors, diagnostics).
    /// </summary>
    internal abstract class SqlOperations : ISqlOperations {

        protected SqlOperations(SqlBridgeOptions options) {
            Options = options;
        }

        protected SqlBridgeOptions Options { get; }

        protected abstract Task<CommandScope> OpenAsync(SqlCall call, CancellationToken ct);

        protected abstract Task<T> RunAsync<T>(SqlCall call,
            Func<SqlCall, CancellationToken, Task<T>> operation, CancellationToken ct);

        public abstract IStoredProcedureBuilder StoredProcedure(string procedureName);

        public abstract IBulkCopyBuilder BulkCopy(string destinationTable);

        public async Task<IReadOnlyList<T>> QueryAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : new() {
            return await RunAsync(SqlCall.From(commandText, parameters), ReadRowsAsync<T>, ct);
        }

        public async Task<T?> QuerySingleAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : class, new() {
            return await RunAsync(SqlCall.From(commandText, parameters), ReadFirstRowAsync<T>, ct);
        }

        public async Task<int> ExecuteAsync(string commandText, object? parameters = null,
            CancellationToken ct = default) {
            return await RunAsync(SqlCall.From(commandText, parameters), RunNonQueryAsync, ct);
        }

        public async Task<object?> ScalarAsync(string commandText, object? parameters = null,
            CancellationToken ct = default) {
            return await RunAsync(SqlCall.From(commandText, parameters), ReadScalarAsync, ct);
        }

        public async IAsyncEnumerable<T> StreamAsync<T>(string commandText, object? parameters = null,
            [EnumeratorCancellation] CancellationToken ct = default) where T : new() {
            var call = SqlCall.From(commandText, parameters);
            await using var scope = await OpenAsync(call, ct);

            var mapper = new ReflectionResultMapper<T>();
            await using var reader = await scope.Command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);

            while (await reader.ReadAsync(ct)) {
                yield return mapper.Map(reader);
            }
        }

        public async Task<StoredProcedureResult<T>> ExecuteWithOutputAsync<T>(string procedureName,
            object? parameters = null, Action<IStoredProcedureBuilder>? configure = null,
            CancellationToken ct = default) where T : new() {
            var builder = StoredProcedure(procedureName);

            foreach (var parameter in ParameterBuilder.FromObject(parameters)) {
                builder.WithParam(parameter.ParameterName, parameter.Value == DBNull.Value ? null : parameter.Value);
            }

            configure?.Invoke(builder);
            return await builder.ExecuteAsync<T>(ct);
        }

        protected SqlCommand CreateCommand(SqlConnection connection, SqlTransaction? transaction, SqlCall call) {
            var command = connection.CreateCommand();
            command.CommandText = call.CommandText;
            command.CommandType = call.CommandType;
            command.CommandTimeout = Options.DefaultCommandTimeout;
            command.Transaction = transaction;

            foreach (var parameter in ParameterBuilder.FromObject(call.Parameters)) {
                command.Parameters.Add(parameter);
            }

            return command;
        }

        private async Task<IReadOnlyList<T>> ReadRowsAsync<T>(SqlCall call, CancellationToken token) where T : new() {
            await using var scope = await OpenAsync(call, token);
            var mapper = new ReflectionResultMapper<T>();
            var results = new List<T>();

            await using var reader = await scope.Command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) {
                results.Add(mapper.Map(reader));
            }

            return results;
        }

        private async Task<T?> ReadFirstRowAsync<T>(SqlCall call, CancellationToken token) where T : class, new() {
            await using var scope = await OpenAsync(call, token);
            var mapper = new ReflectionResultMapper<T>();

            await using var reader = await scope.Command.ExecuteReaderAsync(token);
            return await reader.ReadAsync(token) ? mapper.Map(reader) : null;
        }

        private async Task<int> RunNonQueryAsync(SqlCall call, CancellationToken token) {
            await using var scope = await OpenAsync(call, token);
            return await scope.Command.ExecuteNonQueryAsync(token);
        }

        private async Task<object?> ReadScalarAsync(SqlCall call, CancellationToken token) {
            await using var scope = await OpenAsync(call, token);
            var result = await scope.Command.ExecuteScalarAsync(token);
            return result == DBNull.Value ? null : result;
        }

    }
}
