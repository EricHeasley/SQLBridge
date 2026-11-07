using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SQLBridge.Configuration;
using SQLBridge.Pool;

namespace SQLBridge.Core {
    internal sealed class SqlConnectionFactory : ISqlConnectionFactory {

        private readonly string _connectionString;
        private readonly ConnectionPoolMonitor? _poolMonitor;

        public SqlConnectionFactory(IOptions<SqlClientOptions> options, ConnectionPoolMonitor? poolMonitor = null) {
            _connectionString = options.Value.ConnectionString;
            _poolMonitor = poolMonitor;

            if (string.IsNullOrWhiteSpace(_connectionString)) {
                throw new InvalidOperationException("SqlClient connection string is not configured.");
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
