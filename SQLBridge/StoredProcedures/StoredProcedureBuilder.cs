using System.Data;
using Microsoft.Data.SqlClient;

namespace SQLBridge {
    internal sealed class StoredProcedureBuilder : IStoredProcedureBuilder {

        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly RetryPolicy _retryPolicy;
        private readonly SqlBridgeOptions _options;
        private readonly string _procedureName;
        private readonly List<SqlParameter> _parameters = [];
        private readonly Dictionary<Type, object> _customMappers = new();
        private int? _timeout;
        private bool _captureReturnValue;

        // Overload for use within a transaction
        private readonly SqlConnection? _transactionConnection;
        private readonly SqlTransaction? _transaction;

        internal StoredProcedureBuilder(
            ISqlConnectionFactory connectionFactory,
            RetryPolicy retryPolicy,
            SqlBridgeOptions options,
            string procedureName) {
            _connectionFactory = connectionFactory;
            _retryPolicy = retryPolicy;
            _options = options;
            _procedureName = procedureName;
        }

        internal StoredProcedureBuilder(
            SqlConnection connection,
            SqlTransaction transaction,
            RetryPolicy retryPolicy,
            SqlBridgeOptions options,
            string procedureName) {
            _connectionFactory = null!;
            _transactionConnection = connection;
            _transaction = transaction;
            _retryPolicy = retryPolicy;
            _options = options;
            _procedureName = procedureName;
        }

        public IStoredProcedureBuilder WithParam(string name, object? value) {
            _parameters.Add(new SqlParameter(EnsureAt(name), value ?? DBNull.Value));
            return this;
        }

        public IStoredProcedureBuilder WithParam(string name, object? value, SqlDbType dbType) {
            var param = new SqlParameter(EnsureAt(name), dbType) { Value = value ?? DBNull.Value };
            _parameters.Add(param);
            return this;
        }

        public IStoredProcedureBuilder WithParam(string name, object? value, SqlDbType dbType, int size) {
            var param = new SqlParameter(EnsureAt(name), dbType, size) { Value = value ?? DBNull.Value };
            _parameters.Add(param);
            return this;
        }

        public IStoredProcedureBuilder WithInputOutput(string name, object? value, SqlDbType dbType, int size = 0) {
            var param = new SqlParameter(EnsureAt(name), dbType, size) {
                Direction = ParameterDirection.InputOutput,
                Value = value ?? DBNull.Value
            };
            _parameters.Add(param);
            return this;
        }

        public IStoredProcedureBuilder WithOutputParam(string name, SqlDbType dbType, int size = 0) {
            var param = new SqlParameter(EnsureAt(name), dbType, size) {
                Direction = ParameterDirection.Output
            };
            _parameters.Add(param);
            return this;
        }

        public IStoredProcedureBuilder WithReturnValue() {
            _captureReturnValue = true;
            return this;
        }

        public IStoredProcedureBuilder WithTimeout(int seconds) {
            _timeout = seconds;
            return this;
        }

        public IStoredProcedureBuilder WithMapper<T>(IResultMapper<T> mapper) where T : new() {
            _customMappers[typeof(T)] = mapper;
            return this;
        }

        public async Task<IReadOnlyList<T>> QueryAsync<T>(CancellationToken ct = default) where T : new() {
            return await _retryPolicy.ExecuteAsync(ReadRowsAsync<T>, ct);
        }

        public async Task<T?> QuerySingleAsync<T>(CancellationToken ct = default) where T : class, new() {
            return await _retryPolicy.ExecuteAsync(ReadFirstRowAsync<T>, ct);
        }

        public async Task<(IReadOnlyList<T1>, IReadOnlyList<T2>)> QueryMultipleAsync<T1, T2>(
            CancellationToken ct = default) where T1 : new() where T2 : new() {
            return await _retryPolicy.ExecuteAsync(ReadTwoResultSetsAsync<T1, T2>, ct);
        }

        public async Task<(IReadOnlyList<T1>, IReadOnlyList<T2>, IReadOnlyList<T3>)> QueryMultipleAsync<T1, T2, T3>(
            CancellationToken ct = default) where T1 : new() where T2 : new() where T3 : new() {
            return await _retryPolicy.ExecuteAsync(ReadThreeResultSetsAsync<T1, T2, T3>, ct);
        }

        public async Task<StoredProcedureResult> ExecuteAsync(CancellationToken ct = default) {
            return await _retryPolicy.ExecuteAsync(RunNonQueryAsync, ct);
        }

        public async Task<StoredProcedureResult<T>> ExecuteAsync<T>(CancellationToken ct = default) where T : new() {
            return await _retryPolicy.ExecuteAsync(RunWithResultAsync<T>, ct);
        }

        public async Task<object?> ScalarAsync(CancellationToken ct = default) {
            return await _retryPolicy.ExecuteAsync(ReadScalarAsync, ct);
        }

        public async IAsyncEnumerable<T> StreamAsync<T>(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default) where T : new() {
            var scope = await CreateCommandAsync(ct);
            await using var _ = scope;

            var mapper = GetMapper<T>();
            var reader = await scope.Command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            await using var __ = reader;

            while (await reader.ReadAsync(ct)) {
                yield return mapper.Map(reader);
            }
        }

        private async Task<IReadOnlyList<T>> ReadRowsAsync<T>(CancellationToken token) where T : new() {
            await using var scope = await CreateCommandAsync(token);
            var mapper = GetMapper<T>();
            var results = new List<T>();

            await using var reader = await scope.Command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) {
                results.Add(mapper.Map(reader));
            }

            return results;
        }

        private async Task<T?> ReadFirstRowAsync<T>(CancellationToken token) where T : class, new() {
            await using var scope = await CreateCommandAsync(token);
            var mapper = GetMapper<T>();

            await using var reader = await scope.Command.ExecuteReaderAsync(token);
            return await reader.ReadAsync(token) ? mapper.Map(reader) : null;
        }

        private async Task<(IReadOnlyList<T1>, IReadOnlyList<T2>)> ReadTwoResultSetsAsync<T1, T2>(
            CancellationToken token) where T1 : new() where T2 : new() {
            await using var scope = await CreateCommandAsync(token);
            await using var reader = await scope.Command.ExecuteReaderAsync(token);

            var list1 = await ReadResultSetAsync(reader, GetMapper<T1>(), token);
            await reader.NextResultAsync(token);
            var list2 = await ReadResultSetAsync(reader, GetMapper<T2>(), token);

            return (list1, list2);
        }

        private async Task<(IReadOnlyList<T1>, IReadOnlyList<T2>, IReadOnlyList<T3>)> ReadThreeResultSetsAsync<T1, T2, T3>(
            CancellationToken token) where T1 : new() where T2 : new() where T3 : new() {
            await using var scope = await CreateCommandAsync(token);
            await using var reader = await scope.Command.ExecuteReaderAsync(token);

            var list1 = await ReadResultSetAsync(reader, GetMapper<T1>(), token);
            await reader.NextResultAsync(token);
            var list2 = await ReadResultSetAsync(reader, GetMapper<T2>(), token);
            await reader.NextResultAsync(token);
            var list3 = await ReadResultSetAsync(reader, GetMapper<T3>(), token);

            return (list1, list2, list3);
        }

        private async Task<StoredProcedureResult> RunNonQueryAsync(CancellationToken token) {
            await using var scope = await CreateCommandAsync(token);
            var rows = await scope.Command.ExecuteNonQueryAsync(token);

            var output = new OutputParameterCollection();
            output.Capture(scope.Command.Parameters);

            return new StoredProcedureResult {
                RowsAffected = rows,
                OutputParams = output
            };
        }

        private async Task<StoredProcedureResult<T>> RunWithResultAsync<T>(CancellationToken token) where T : new() {
            await using var scope = await CreateCommandAsync(token);
            var mapper = GetMapper<T>();
            var results = new List<T>();

            await using var reader = await scope.Command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) {
                results.Add(mapper.Map(reader));
            }

            // Output parameters are only available once the reader is closed
            await reader.CloseAsync();

            var output = new OutputParameterCollection();
            output.Capture(scope.Command.Parameters);

            return new StoredProcedureResult<T> {
                Data = results,
                RowsAffected = reader.RecordsAffected,
                OutputParams = output
            };
        }

        private async Task<object?> ReadScalarAsync(CancellationToken token) {
            await using var scope = await CreateCommandAsync(token);
            var result = await scope.Command.ExecuteScalarAsync(token);
            return result == DBNull.Value ? null : result;
        }

        private IResultMapper<T> GetMapper<T>() where T : new() {
            if (_customMappers.TryGetValue(typeof(T), out var mapper)) {
                return (IResultMapper<T>)mapper;
            }

            return new ReflectionResultMapper<T>();
        }

        private async Task<CommandScope> CreateCommandAsync(CancellationToken ct) {
            SqlConnection connection;
            bool ownsConnection;

            if (_transactionConnection is not null) {
                connection = _transactionConnection;
                ownsConnection = false;
            } else {
                connection = await _connectionFactory.CreateOpenConnectionAsync(ct);
                ownsConnection = true;
            }

            var command = connection.CreateCommand();
            command.CommandText = _procedureName;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = _timeout ?? _options.DefaultCommandTimeout;
            command.Transaction = _transaction;

            foreach (var param in _parameters) {
                command.Parameters.Add(param);
            }

            if (_captureReturnValue) {
                command.Parameters.Add(new SqlParameter("@RETURN_VALUE", SqlDbType.Int) {
                    Direction = ParameterDirection.ReturnValue
                });
            }

            return new CommandScope(command, ownsConnection ? connection : null);
        }

        private static async Task<IReadOnlyList<T>> ReadResultSetAsync<T>(
            SqlDataReader reader, IResultMapper<T> mapper, CancellationToken ct) {
            var results = new List<T>();
            while (await reader.ReadAsync(ct)) {
                results.Add(mapper.Map(reader));
            }
            return results;
        }

        private static string EnsureAt(string name) {
            return name.StartsWith('@') ? name : $"@{name}";
        }

    }
}
