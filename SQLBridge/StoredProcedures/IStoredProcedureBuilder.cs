using System.Data;

namespace SQLBridge {
    public interface IStoredProcedureBuilder {

        IStoredProcedureBuilder WithParam(string name, object? value);
        IStoredProcedureBuilder WithParam(string name, object? value, SqlDbType dbType);
        IStoredProcedureBuilder WithParam(string name, object? value, SqlDbType dbType, int size);
        IStoredProcedureBuilder WithInputOutput(string name, object? value, SqlDbType dbType, int size = 0);
        IStoredProcedureBuilder WithOutputParam(string name, SqlDbType dbType, int size = 0);
        IStoredProcedureBuilder WithReturnValue();
        IStoredProcedureBuilder WithTimeout(int seconds);
        IStoredProcedureBuilder WithMapper<T>(IResultMapper<T> mapper) where T : new();

        Task<IReadOnlyList<T>> QueryAsync<T>(CancellationToken ct = default) where T : new();
        Task<T?> QuerySingleAsync<T>(CancellationToken ct = default) where T : class, new();

        Task<(IReadOnlyList<T1>, IReadOnlyList<T2>)> QueryMultipleAsync<T1, T2>(
            CancellationToken ct = default) where T1 : new() where T2 : new();

        Task<(IReadOnlyList<T1>, IReadOnlyList<T2>, IReadOnlyList<T3>)> QueryMultipleAsync<T1, T2, T3>(
            CancellationToken ct = default) where T1 : new() where T2 : new() where T3 : new();

        Task<StoredProcedureResult> ExecuteAsync(CancellationToken ct = default);
        Task<StoredProcedureResult<T>> ExecuteAsync<T>(CancellationToken ct = default) where T : new();
        Task<object?> ScalarAsync(CancellationToken ct = default);

        // === Streaming ===
        IAsyncEnumerable<T> StreamAsync<T>(CancellationToken ct = default) where T : new();

    }
}
