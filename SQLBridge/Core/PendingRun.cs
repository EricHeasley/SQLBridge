namespace SQLBridge {
    /// <summary>
    /// A call together with the operation that will execute it, handed through the interceptor pipeline.
    /// </summary>
    internal sealed record PendingRun<T>(SqlCall Call, Func<SqlCall, CancellationToken, Task<T>> Operation);
}
