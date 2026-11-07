using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace SQLBridge {
    internal sealed class SqlConnectionFactory : ISqlConnectionFactory {

        private readonly string _connectionString;
        private readonly ConnectionPoolMonitor? _poolMonitor;

        public SqlConnectionFactory(string connectionString, ConnectionPoolMonitor? poolMonitor = null) {
            _connectionString = connectionString;
            _poolMonitor = poolMonitor;

            if (string.IsNullOrWhiteSpace(_connectionString)) {
                throw new InvalidOperationException("SQLBridge connection string is not configured.");
            }
        }

        public async Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken ct = default) {
            var sw = Stopwatch.StartNew();
            var connection = new SqlConnection(_connectionString);

            try {
                await connection.OpenAsync(ct);
                sw.Stop();
                _poolMonitor?.RecordOpen(sw.Elapsed);
                return connection;
            } catch {
                sw.Stop();
                _poolMonitor?.RecordFailure();
                await connection.DisposeAsync();
                throw;
            }
        }

    }
}
