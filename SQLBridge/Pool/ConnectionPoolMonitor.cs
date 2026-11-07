using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SQLBridge.Configuration;

namespace SQLBridge.Pool {
    internal sealed class ConnectionPoolMonitor : IConnectionPoolMonitor {

        private readonly string _connectionString;
        private long _totalOpened;
        private long _totalFailed;
        private long _totalOpenTimeMs;

        public ConnectionPoolMonitor(IOptions<SqlClientOptions> options) {
            _connectionString = options.Value.ConnectionString;
        }

        internal void RecordOpen(TimeSpan elapsed) {
            Interlocked.Increment(ref _totalOpened);
            Interlocked.Add(ref _totalOpenTimeMs, (long)elapsed.TotalMilliseconds);
        }

        internal void RecordFailure() {
            Interlocked.Increment(ref _totalFailed);
        }

        public ConnectionPoolStats GetStats() {
            var totalOpened = Interlocked.Read(ref _totalOpened);
            var totalFailed = Interlocked.Read(ref _totalFailed);
            var totalOpenTimeMs = Interlocked.Read(ref _totalOpenTimeMs);

            // ADO.NET doesn't expose pool internals directly, but we can query
            // performance counters through a test connection
            int active = 0, idle = 0, total = 0;

            try {
                // Use SqlConnection.RetrieveInternalInfo or SqlClientEventSource
                // For now, use our tracked metrics
                using var conn = new SqlConnection(_connectionString);
                // The pool stats are at the connection string level
                // We can get them from the internal connection pool via reflection if needed
            } catch {
                // Ignore — return tracked metrics only
            }

            var avgOpenTime = totalOpened > 0
                ? TimeSpan.FromMilliseconds((double)totalOpenTimeMs / totalOpened)
                : TimeSpan.Zero;

            return new ConnectionPoolStats(
                ActiveConnections: active,
                IdleConnections: idle,
                TotalConnections: total,
                TotalConnectionsOpened: totalOpened,
                TotalConnectionsFailed: totalFailed,
                AverageOpenTime: avgOpenTime);
        }

        public void ClearPool() {
            using var connection = new SqlConnection(_connectionString);
            SqlConnection.ClearPool(connection);
        }

    }
}
