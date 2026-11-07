using Microsoft.Extensions.Diagnostics.HealthChecks;
using SQLBridge.Core;

namespace SQLBridge.Diagnostics {
    public sealed class SqlClientHealthCheck : IHealthCheck {

        private readonly ISqlConnectionFactory _connectionFactory;

        public SqlClientHealthCheck(ISqlConnectionFactory connectionFactory) {
            _connectionFactory = connectionFactory;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default) {
            try {
                await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

                // Run a trivial query to validate the connection is usable
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                command.CommandTimeout = 5;
                await command.ExecuteScalarAsync(cancellationToken);

                return HealthCheckResult.Healthy("SQL Server connection is healthy.");
            } catch (Exception ex) {
                return HealthCheckResult.Unhealthy(
                    "SQL Server connection failed.",
                    exception: ex);
            }
        }

    }
}
