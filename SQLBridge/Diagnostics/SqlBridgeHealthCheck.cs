using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SQLBridge {
    internal sealed class SqlBridgeHealthCheck : IHealthCheck {

        private readonly ISqlClient _client;

        public SqlBridgeHealthCheck(ISqlClient client) {
            _client = client;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default) {
            try {
                await _client.PingAsync(cancellationToken);
                return HealthCheckResult.Healthy("SQL Server connection is healthy.");
            } catch (Exception ex) {
                return HealthCheckResult.Unhealthy(
                    "SQL Server connection failed.",
                    exception: ex);
            }
        }

    }
}
