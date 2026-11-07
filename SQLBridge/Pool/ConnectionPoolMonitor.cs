using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace SQLBridge {
    internal sealed class ConnectionPoolMonitor : IConnectionPoolMonitor {

        private readonly string _connectionString;
        private long _totalOpened;
        private long _totalFailed;
        private long _totalOpenTimeMs;

        public ConnectionPoolMonitor(string connectionString) {
            _connectionString = connectionString;
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

            var averageOpenTime = totalOpened > 0
                ? TimeSpan.FromMilliseconds((double)totalOpenTimeMs / totalOpened)
                : TimeSpan.Zero;

            return new ConnectionPoolStats(
                TotalConnectionsOpened: totalOpened,
                TotalConnectionsFailed: totalFailed,
                AverageOpenTime: averageOpenTime);
        }

        public void ClearPool() {
            using var connection = new SqlConnection(_connectionString);
            SqlConnection.ClearPool(connection);
        }

    }
}
