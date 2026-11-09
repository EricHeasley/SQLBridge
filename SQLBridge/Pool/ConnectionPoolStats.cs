namespace SQLBridge {
    /// <summary>
    /// Counters for connections opened through SQLBridge, from <see cref="ISqlClient.GetPoolStats"/>. ADO.NET does not expose live pool sizes,
    /// so only what SQLBridge itself observes is reported.
    /// </summary>
    public sealed record ConnectionPoolStats(
        long TotalConnectionsOpened,
        long TotalConnectionsFailed,
        TimeSpan AverageOpenTime);
}
