namespace SQLBridge {
    /// <summary>
    /// An open database transaction with the same operations as the client. Dispose it to release the
    /// connection; a transaction that is not committed is rolled back.
    /// </summary>
    /// <remarks>Statements run once inside a transaction; they are not retried.</remarks>
    public interface ISqlTransaction : ISqlOperations, IAsyncDisposable {

        /// <summary>Commits all work done in this transaction.</summary>
        Task CommitAsync(CancellationToken ct = default);

        /// <summary>Rolls back all work done in this transaction.</summary>
        Task RollbackAsync(CancellationToken ct = default);

    }
}
