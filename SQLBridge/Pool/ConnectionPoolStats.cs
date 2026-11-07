namespace SQLBridge.Pool {
    public sealed record ConnectionPoolStats(
        int ActiveConnections,
        int IdleConnections,
        int TotalConnections,
        long TotalConnectionsOpened,
        long TotalConnectionsFailed,
        TimeSpan AverageOpenTime);
}
