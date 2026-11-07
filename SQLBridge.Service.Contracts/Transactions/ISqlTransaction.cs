using SQLBridge.Service.Contracts.StoredProcedures;

namespace SQLBridge.Service.Contracts.Transactions {
    public interface ISqlTransaction : IAsyncDisposable {

        Task<IReadOnlyList<T>> ExecAsync<T>(string procedureName, object? parameters = null,
            CancellationToken ct = default) where T : new();

        Task<int> ExecNonQueryAsync(string procedureName, object? parameters = null,
            CancellationToken ct = default);

        Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null,
            CancellationToken ct = default) where T : new();

        Task<int> ExecuteAsync(string sql, object? parameters = null,
            CancellationToken ct = default);

        IStoredProcedureBuilder StoredProc(string procedureName);

        Task CommitAsync(CancellationToken ct = default);
        Task RollbackAsync(CancellationToken ct = default);

    }
}
