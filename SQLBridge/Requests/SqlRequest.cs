using System.Data;
using Microsoft.Data.SqlClient;

namespace SQLBridge {
    internal sealed class SqlRequest : ISqlRequest {

        private readonly SqlOperations _operations;
        private readonly string _commandText;
        private readonly List<ParameterSpec> _specs = [];
        private readonly Dictionary<Type, object> _mappers = new();
        private int? _timeoutSeconds;
        private bool _cache;
        private TimeSpan? _cacheTtl;

        public SqlRequest(SqlOperations operations, string commandText) {
            _operations = operations;
            _commandText = commandText;
        }

        public ISqlRequest WithParam(string name, object? value) {
            _specs.Add(ParameterSpec.Input(name, value));
            return this;
        }

        public ISqlRequest WithParam(string name, object? value, SqlDbType dbType) {
            return WithParam(name, value, dbType, 0);
        }

        public ISqlRequest WithParam(string name, object? value, SqlDbType dbType, int size) {
            _specs.Add(new ParameterSpec(ParameterBuilder.EnsureAtPrefix(name), value, dbType, size, ParameterDirection.Input));
            return this;
        }

        public ISqlRequest WithParams(object? parameters) {
            var carriesTypes = parameters is IEnumerable<SqlParameter>;
            foreach (var parameter in ParameterBuilder.FromObject(parameters)) {
                if (carriesTypes) {
                    _specs.Add(ParameterSpec.FromParameter(parameter));
                } else {
                    _specs.Add(ParameterSpec.Input(parameter.ParameterName, parameter.Value == DBNull.Value ? null : parameter.Value));
                }
            }

            return this;
        }

        public ISqlRequest WithInputOutput(string name, object? value, SqlDbType dbType, int size = 0) {
            _specs.Add(new ParameterSpec(ParameterBuilder.EnsureAtPrefix(name), value, dbType, size, ParameterDirection.InputOutput));
            return this;
        }

        public ISqlRequest WithOutputParam(string name, SqlDbType dbType, int size = 0) {
            _specs.Add(new ParameterSpec(ParameterBuilder.EnsureAtPrefix(name), null, dbType, size, ParameterDirection.Output));
            return this;
        }

        public ISqlRequest WithReturnValue() {
            _specs.Add(new ParameterSpec("@RETURN_VALUE", null, SqlDbType.Int, 0, ParameterDirection.ReturnValue));
            return this;
        }

        public ISqlRequest WithTimeout(int seconds) {
            _timeoutSeconds = seconds;
            return this;
        }

        public ISqlRequest MapWith<T>(IResultMapper<T> mapper) where T : new() {
            _mappers[typeof(T)] = mapper;
            return this;
        }

        public ISqlRequest CacheFor(TimeSpan? ttl = null) {
            _cache = true;
            _cacheTtl = ttl;
            return this;
        }

        public async Task<IReadOnlyList<T>> QueryAsync<T>(CancellationToken ct = default) where T : new() {
            if (_cache) {
                return await _operations.RunCachedQueryAsync<T>(BuildCall(), _cacheTtl, ct);
            }

            return await _operations.RunQueryAsync<T>(BuildCall(), ct);
        }

        public async Task<T?> QuerySingleAsync<T>(CancellationToken ct = default) where T : class, new() {
            RejectCaching(nameof(QuerySingleAsync));
            return await _operations.RunQuerySingleAsync<T>(BuildCall(), ct);
        }

        public async Task<(IReadOnlyList<T1>, IReadOnlyList<T2>)> QueryMultipleAsync<T1, T2>(
            CancellationToken ct = default) where T1 : new() where T2 : new() {
            RejectCaching(nameof(QueryMultipleAsync));
            return await _operations.RunQueryMultipleAsync<T1, T2>(BuildCall(), ct);
        }

        public async Task<(IReadOnlyList<T1>, IReadOnlyList<T2>, IReadOnlyList<T3>)> QueryMultipleAsync<T1, T2, T3>(
            CancellationToken ct = default) where T1 : new() where T2 : new() where T3 : new() {
            RejectCaching(nameof(QueryMultipleAsync));
            return await _operations.RunQueryMultipleAsync<T1, T2, T3>(BuildCall(), ct);
        }

        public async Task<CommandResult> ExecuteAsync(CancellationToken ct = default) {
            RejectCaching(nameof(ExecuteAsync));
            return await _operations.RunExecuteAsync(BuildCall(), ct);
        }

        public async Task<CommandResult<T>> ExecuteAsync<T>(CancellationToken ct = default) where T : new() {
            RejectCaching(nameof(ExecuteAsync));
            return await _operations.RunExecuteAsync<T>(BuildCall(), ct);
        }

        public async Task<object?> ScalarAsync(CancellationToken ct = default) {
            RejectCaching(nameof(ScalarAsync));
            return await _operations.RunScalarAsync(BuildCall(), ct);
        }

        public IAsyncEnumerable<T> StreamAsync<T>(CancellationToken ct = default) where T : new() {
            RejectCaching(nameof(StreamAsync));
            return _operations.StreamRowsAsync<T>(BuildCall(), ct);
        }

        private SqlCall BuildCall() {
            return new SqlCall(
                _commandText,
                CommandTypeResolver.Resolve(_commandText),
                null,
                _specs.ToArray(),
                _timeoutSeconds,
                new Dictionary<Type, object>(_mappers));
        }

        private void RejectCaching(string terminalCall) {
            if (_cache) {
                throw new InvalidOperationException(
                    $"CacheFor applies only to QueryAsync, but {terminalCall} was called on a cached request.");
            }
        }

    }
}
