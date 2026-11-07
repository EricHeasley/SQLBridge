using Microsoft.Extensions.Options;

namespace SQLBridge {
    /// <summary>
    /// Sets the connection string for <c>AddSqlBridge(connectionString)</c>.
    /// </summary>
    internal sealed class SqlBridgeConnectionStringSetup : IConfigureOptions<SqlBridgeOptions> {

        private readonly string _connectionString;

        public SqlBridgeConnectionStringSetup(string connectionString) {
            _connectionString = connectionString;
        }

        public void Configure(SqlBridgeOptions options) {
            options.ConnectionString = _connectionString;
        }

    }
}
