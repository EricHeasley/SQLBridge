using System.Data;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;

namespace SQLBridge {
    /// <summary>
    /// Shared implementation of <see cref="ISqlOperations"/>. The client and a transaction differ only in
    /// where commands come from and how a run is wrapped (retry, interceptors, diagnostics, caching).
    /// </summary>
    internal abstract class SqlOperations : ISqlOperations {

        protected SqlOperations(SqlBridgeOptions options) {
            Options = options;
        }

        protected SqlBridgeOptions Options { get; }

        protected abstract Task<CommandScope> OpenAsync(SqlCall call, CancellationToken ct);

        protected abstract Task<T> RunAsync<T>(SqlCall call,
            Func<SqlCall, CancellationToken, Task<T>> operation, CancellationToken ct);

        public abstract IBulkCopyBuilder BulkCopy(string destinationTable);

        // === Simple API ===

        public async Task<IReadOnlyList<T>> QueryAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : new() {
            return await RunQueryAsync<T>(SqlCall.From(commandText, parameters), ct);
        }

        public async Task<T?> QuerySingleAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : class, new() {
            return await RunQuerySingleAsync<T>(SqlCall.From(commandText, parameters), ct);
        }

        public async Task<int> ExecuteAsync(string commandText, object? parameters = null,
            CancellationToken ct = default) {
            return await RunAsync(SqlCall.From(commandText, parameters), RunNonQueryAsync, ct);
        }

        public async Task<object?> ScalarAsync(string commandText, object? parameters = null,
            CancellationToken ct = default) {
            return await RunScalarAsync(SqlCall.From(commandText, parameters), ct);
        }

        public IAsyncEnumerable<T> StreamAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : new() {
            return StreamRowsAsync<T>(SqlCall.From(commandText, parameters), ct);
        }

        public ISqlRequest Command(string commandText) {
            return new SqlRequest(this, commandText);
        }

        // === Used by SqlRequest ===

        internal async Task<IReadOnlyList<T>> RunQueryAsync<T>(SqlCall call, CancellationToken ct) where T : new() {
            return await RunAsync(call, ReadRowsAsync<T>, ct);
        }

        internal virtual Task<IReadOnlyList<T>> RunCachedQueryAsync<T>(SqlCall call, TimeSpan? ttl,
            CancellationToken ct) where T : new() {
            throw new InvalidOperationException("Caching is not available inside a transaction.");
        }

        internal async Task<T?> RunQuerySingleAsync<T>(SqlCall call, CancellationToken ct) where T : class, new() {
            return await RunAsync(call, ReadFirstRowAsync<T>, ct);
        }

        internal async Task<(IReadOnlyList<T1>, IReadOnlyList<T2>)> RunQueryMultipleAsync<T1, T2>(
            SqlCall call, CancellationToken ct) where T1 : new() where T2 : new() {
            return await RunAsync(call, ReadTwoResultSetsAsync<T1, T2>, ct);
        }

        internal async Task<(IReadOnlyList<T1>, IReadOnlyList<T2>, IReadOnlyList<T3>)> RunQueryMultipleAsync<T1, T2, T3>(
            SqlCall call, CancellationToken ct) where T1 : new() where T2 : new() where T3 : new() {
            return await RunAsync(call, ReadThreeResultSetsAsync<T1, T2, T3>, ct);
        }

        internal async Task<CommandResult> RunExecuteAsync(SqlCall call, CancellationToken ct) {
            return await RunAsync(call, ExecuteWithOutputAsync, ct);
        }

        internal async Task<CommandResult<T>> RunExecuteAsync<T>(SqlCall call, CancellationToken ct) where T : new() {
            return await RunAsync(call, ExecuteWithRowsAsync<T>, ct);
        }

        internal async Task<object?> RunScalarAsync(SqlCall call, CancellationToken ct) {
            return await RunAsync(call, ReadScalarAsync, ct);
        }

        internal async IAsyncEnumerable<T> StreamRowsAsync<T>(SqlCall call,
            [EnumeratorCancellation] CancellationToken ct) where T : new() {
            await using var scope = await OpenAsync(call, ct);

            var mapper = GetMapper<T>(call);
            await using var reader = await scope.Command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);

            while (await reader.ReadAsync(ct)) {
                yield return mapper.Map(reader);
            }
        }

        // === Building commands ===

        protected SqlCommand CreateCommand(SqlConnection connection, SqlTransaction? transaction, SqlCall call) {
            var command = connection.CreateCommand();
            command.CommandText = call.CommandText;
            command.CommandType = call.CommandType;
            command.CommandTimeout = call.TimeoutSeconds ?? Options.DefaultCommandTimeout;
            command.Transaction = transaction;

            foreach (var parameter in ParameterBuilder.FromObject(call.Parameters)) {
                command.Parameters.Add(parameter);
            }

            foreach (var spec in call.Specs) {
                command.Parameters.Add(spec.ToSqlParameter());
            }

            return command;
        }

        /// <summary>A mapper for this call, else one registered in the options, else name-based mapping.</summary>
        private IResultMapper<T> GetMapper<T>(SqlCall call) where T : new() {
            if (call.Mappers.TryGetValue(typeof(T), out var perCall)) {
                return (IResultMapper<T>)perCall;
            }

            if (Options.Mappers.TryGetValue(typeof(T), out var registered)) {
                return (IResultMapper<T>)registered;
            }

            return new ReflectionResultMapper<T>();
        }

        // === Operations: each runs one command and reads its result ===

        private async Task<IReadOnlyList<T>> ReadRowsAsync<T>(SqlCall call, CancellationToken token) where T : new() {
            await using var scope = await OpenAsync(call, token);
            var mapper = GetMapper<T>(call);

            await using var reader = await scope.Command.ExecuteReaderAsync(token);
            return await ReadResultSetAsync(reader, mapper, token);
        }

        private async Task<T?> ReadFirstRowAsync<T>(SqlCall call, CancellationToken token) where T : class, new() {
            await using var scope = await OpenAsync(call, token);
            var mapper = GetMapper<T>(call);

            await using var reader = await scope.Command.ExecuteReaderAsync(token);
            return await reader.ReadAsync(token) ? mapper.Map(reader) : null;
        }

        private async Task<(IReadOnlyList<T1>, IReadOnlyList<T2>)> ReadTwoResultSetsAsync<T1, T2>(
            SqlCall call, CancellationToken token) where T1 : new() where T2 : new() {
            await using var scope = await OpenAsync(call, token);
            await using var reader = await scope.Command.ExecuteReaderAsync(token);

            var list1 = await ReadResultSetAsync(reader, GetMapper<T1>(call), token);
            await reader.NextResultAsync(token);
            var list2 = await ReadResultSetAsync(reader, GetMapper<T2>(call), token);

            return (list1, list2);
        }

        private async Task<(IReadOnlyList<T1>, IReadOnlyList<T2>, IReadOnlyList<T3>)> ReadThreeResultSetsAsync<T1, T2, T3>(
            SqlCall call, CancellationToken token) where T1 : new() where T2 : new() where T3 : new() {
            await using var scope = await OpenAsync(call, token);
            await using var reader = await scope.Command.ExecuteReaderAsync(token);

            var list1 = await ReadResultSetAsync(reader, GetMapper<T1>(call), token);
            await reader.NextResultAsync(token);
            var list2 = await ReadResultSetAsync(reader, GetMapper<T2>(call), token);
            await reader.NextResultAsync(token);
            var list3 = await ReadResultSetAsync(reader, GetMapper<T3>(call), token);

            return (list1, list2, list3);
        }

        private async Task<int> RunNonQueryAsync(SqlCall call, CancellationToken token) {
            await using var scope = await OpenAsync(call, token);
            return await scope.Command.ExecuteNonQueryAsync(token);
        }

        private async Task<CommandResult> ExecuteWithOutputAsync(SqlCall call, CancellationToken token) {
            await using var scope = await OpenAsync(call, token);
            var rows = await scope.Command.ExecuteNonQueryAsync(token);

            var output = new OutputParameterCollection();
            output.Capture(scope.Command.Parameters);

            return new CommandResult {
                RowsAffected = rows,
                OutputParams = output
            };
        }

        private async Task<CommandResult<T>> ExecuteWithRowsAsync<T>(SqlCall call, CancellationToken token) where T : new() {
            await using var scope = await OpenAsync(call, token);
            var mapper = GetMapper<T>(call);

            await using var reader = await scope.Command.ExecuteReaderAsync(token);
            var rows = await ReadResultSetAsync(reader, mapper, token);

            // Output parameters are only available once the reader is closed
            await reader.CloseAsync();

            var output = new OutputParameterCollection();
            output.Capture(scope.Command.Parameters);

            return new CommandResult<T> {
                Data = rows,
                RowsAffected = reader.RecordsAffected,
                OutputParams = output
            };
        }

        private async Task<object?> ReadScalarAsync(SqlCall call, CancellationToken token) {
            await using var scope = await OpenAsync(call, token);
            var result = await scope.Command.ExecuteScalarAsync(token);
            return result == DBNull.Value ? null : result;
        }

        private static async Task<IReadOnlyList<T>> ReadResultSetAsync<T>(
            SqlDataReader reader, IResultMapper<T> mapper, CancellationToken token) {
            var results = new List<T>();
            while (await reader.ReadAsync(token)) {
                results.Add(mapper.Map(reader));
            }

            return results;
        }

    }
}
