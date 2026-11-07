namespace SQLBridge {
    public interface ISqlTransaction : ISqlOperations, IAsyncDisposable {

        Task CommitAsync(CancellationToken ct = default);

        Task RollbackAsync(CancellationToken ct = default);

    }
}
