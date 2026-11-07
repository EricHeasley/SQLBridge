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

        protected abstract Task<CommandScope> OpenAsync(string commandText, CommandType commandType,
            object? parameters, CancellationToken ct);

        protected abstract Task<T> RunAsync<T>(string commandText, CommandType commandType,
            Func<CancellationToken, Task<T>> action, CancellationToken ct);

        public abstract IStoredProcedureBuilder StoredProcedure(string procedureName);

        public abstract IBulkCopyBuilder BulkCopy(string destinationTable);

        public async Task<IReadOnlyList<T>> QueryAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : new() {
            var commandType = CommandTypeResolver.Resolve(commandText);
            return await RunAsync(commandText, commandType, async token => {
                await using var scope = await OpenAsync(commandText, commandType, parameters, token);
                var mapper = new ReflectionResultMapper<T>();
                var results = new List<T>();

                await using var reader = await scope.Command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) {
                    results.Add(mapper.Map(reader));
                }

                return (IReadOnlyList<T>)results;
            }, ct);
        }

        public async Task<T?> QuerySingleAsync<T>(string commandText, object? parameters = null,
            CancellationToken ct = default) where T : class, new() {
            var commandType = CommandTypeResolver.Resolve(commandText);
            return await RunAsync(commandText, commandType, async token => {
                await using var scope = await OpenAsync(commandText, commandType, parameters, token);
                var mapper = new ReflectionResultMapper<T>();

                await using var reader = await scope.Command.ExecuteReaderAsync(token);
                return await reader.ReadAsync(token) ? mapper.Map(reader) : null;
            }, ct);
        }

        public async Task<int> ExecuteAsync(string commandText, object? parameters = null,
            CancellationToken ct = default) {
            var commandType = CommandTypeResolver.Resolve(commandText);
            return await RunAsync(commandText, commandType, async token => {
                await using var scope = await OpenAsync(commandText, commandType, parameters, token);
                return await scope.Command.ExecuteNonQueryAsync(token);
            }, ct);
        }

        public async Task<object?> ScalarAsync(string commandText, object? parameters = null,
            CancellationToken ct = default) {
            var commandType = CommandTypeResolver.Resolve(commandText);
            return await RunAsync(commandText, commandType, async token => {
                await using var scope = await OpenAsync(commandText, commandType, parameters, token);
                var result = await scope.Command.ExecuteScalarAsync(token);
                return result == DBNull.Value ? null : result;
            }, ct);
        }

        public async IAsyncEnumerable<T> StreamAsync<T>(string commandText, object? parameters = null,
            [EnumeratorCancellation] CancellationToken ct = default) where T : new() {
            var commandType = CommandTypeResolver.Resolve(commandText);
            await using var scope = await OpenAsync(commandText, commandType, parameters, ct);

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

        protected SqlCommand CreateCommand(SqlConnection connection, SqlTransaction? transaction,
            string commandText, CommandType commandType, object? parameters) {
            var command = connection.CreateCommand();
            command.CommandText = commandText;
            command.CommandType = commandType;
            command.CommandTimeout = Options.DefaultCommandTimeout;
            command.Transaction = transaction;

            foreach (var parameter in ParameterBuilder.FromObject(parameters)) {
                command.Parameters.Add(parameter);
            }

            return command;
        }

    }
}
