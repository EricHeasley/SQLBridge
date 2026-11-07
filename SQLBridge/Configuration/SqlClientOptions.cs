using SQLBridge.Diagnostics;

namespace SQLBridge.Configuration {
    public sealed class SqlClientOptions {
        public string ConnectionString { get; set; } = string.Empty;
        public int DefaultCommandTimeout { get; set; } = 30;
        public RetryOptions Retry { get; set; } = new();

        /// <summary>
        /// Optional callback invoked after every query with timing and result info.
        /// Use for logging slow queries, APM integration, or custom metrics.
        /// </summary>
        public QueryDiagnosticsHandler? OnQueryExecuted { get; set; }

        /// <summary>
        /// Queries exceeding this threshold are logged as warnings. Default: 1 second.
        /// </summary>
        public TimeSpan SlowQueryThreshold { get; set; } = TimeSpan.FromSeconds(1);
    }
}
