using System.Data;
using Microsoft.Data.SqlClient;
using SQLBridge.Model.Configuration;
using SQLBridge.Model.StoredProcedures;
using SQLBridge.Service.Contracts.Core;
using SQLBridge.Service.Contracts.Mapping;
using SQLBridge.Service.Contracts.StoredProcedures;
using SQLBridge.Service.Mapping;
using SQLBridge.Service.Resilience;

namespace SQLBridge.Service.StoredProcedures {
    internal sealed class StoredProcedureBuilder : IStoredProcedureBuilder {

        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly RetryPolicy _retryPolicy;
        private readonly SqlClientOptions _options;
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
            SqlClientOptions options,
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
            SqlClientOptions options,
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
            return await _retryPolicy.ExecuteAsync(async token => {
                await using var scope = await CreateCommandAsync(token);
                var mapper = GetMapper<T>();
                var results = new List<T>();

                await using var reader = await scope.Command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) {
                    results.Add(mapper.Map(reader));
                }

                return (IReadOnlyList<T>)results;
            }, ct);
        }

        public async Task<T?> QuerySingleAsync<T>(CancellationToken ct = default) where T : class, new() {
            return await _retryPolicy.ExecuteAsync(async token => {
                await using var scope = await CreateCommandAsync(token);
                var mapper = GetMapper<T>();

                await using var reader = await scope.Command.ExecuteReaderAsync(token);
                return await reader.ReadAsync(token) ? mapper.Map(reader) : null;
            }, ct);
        }

        public async Task<(IReadOnlyList<T1>, IReadOnlyList<T2>)> QueryMultipleAsync<T1, T2>(
            CancellationToken ct = default) where T1 : new() where T2 : new() {
            return await _retryPolicy.ExecuteAsync(async token => {
                await using var scope = await CreateCommandAsync(token);
                await using var reader = await scope.Command.ExecuteReaderAsync(token);

                var list1 = await ReadResultSetAsync(reader, GetMapper<T1>(), token);
                await reader.NextResultAsync(token);
                var list2 = await ReadResultSetAsync(reader, GetMapper<T2>(), token);

                return ((IReadOnlyList<T1>)list1, (IReadOnlyList<T2>)list2);
            }, ct);
        }

        public async Task<(IReadOnlyList<T1>, IReadOnlyList<T2>, IReadOnlyList<T3>)> QueryMultipleAsync<T1, T2, T3>(
            CancellationToken ct = default) where T1 : new() where T2 : new() where T3 : new() {
            return await _retryPolicy.ExecuteAsync(async token => {
                await using var scope = await CreateCommandAsync(token);
                await using var reader = await scope.Command.ExecuteReaderAsync(token);

                var list1 = await ReadResultSetAsync(reader, GetMapper<T1>(), token);
                await reader.NextResultAsync(token);
                var list2 = await ReadResultSetAsync(reader, GetMapper<T2>(), token);
                await reader.NextResultAsync(token);
                var list3 = await ReadResultSetAsync(reader, GetMapper<T3>(), token);

                return ((IReadOnlyList<T1>)list1, (IReadOnlyList<T2>)list2, (IReadOnlyList<T3>)list3);
            }, ct);
        }

        public async Task<SprocResult> ExecuteAsync(CancellationToken ct = default) {
            return await _retryPolicy.ExecuteAsync(async token => {
                await using var scope = await CreateCommandAsync(token);
                var rows = await scope.Command.ExecuteNonQueryAsync(token);

                var output = new OutputParameterCollection();
                output.Capture(scope.Command.Parameters);

                return new SprocResult { RowsAffected = rows, OutputParams = output };
            }, ct);
        }

        public async Task<SprocResult<T>> ExecuteAsync<T>(CancellationToken ct = default) where T : new() {
            return await _retryPolicy.ExecuteAsync(async token => {
                await using var scope = await CreateCommandAsync(token);
                var mapper = GetMapper<T>();
                var results = new List<T>();
                var rows = 0;

                await using var reader = await scope.Command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) {
                    results.Add(mapper.Map(reader));
                }

                // Close reader to access output params
                await reader.CloseAsync();
                rows = reader.RecordsAffected;

                var output = new OutputParameterCollection();
                output.Capture(scope.Command.Parameters);

                return new SprocResult<T> { Data = results, RowsAffected = rows, OutputParams = output };
            }, ct);
        }

        public async Task<object?> ScalarAsync(CancellationToken ct = default) {
            return await _retryPolicy.ExecuteAsync(async token => {
                await using var scope = await CreateCommandAsync(token);
                var result = await scope.Command.ExecuteScalarAsync(token);
                return result == DBNull.Value ? null : result;
            }, ct);
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

        private static async Task<List<T>> ReadResultSetAsync<T>(
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

        /// <summary>
        /// Manages the lifetime of a command and optionally its connection.
        /// </summary>
        private sealed class CommandScope : IAsyncDisposable {

            private readonly SqlConnection? _ownedConnection;
            public SqlCommand Command { get; }

            public CommandScope(SqlCommand command, SqlConnection? ownedConnection) {
                Command = command;
                _ownedConnection = ownedConnection;
            }

            public async ValueTask DisposeAsync() {
                await Command.DisposeAsync();
                if (_ownedConnection is not null) {
                    await _ownedConnection.DisposeAsync();
                }
            }

        }

    }
}
