namespace SQLBridge {
    public sealed class SqlBridgeOptions {

        public string ConnectionString { get; set; } = string.Empty;
        public int DefaultCommandTimeout { get; set; } = 30;
        public RetryOptions Retry { get; set; } = new();

        /// <summary>
        /// Query result caching used by QueryCachedAsync. Set Enabled to false to bypass the cache.
        /// </summary>
        public CacheOptions Caching { get; set; } = new();

        /// <summary>
        /// Track connection open counts and timings, exposed through ISqlClient.Pool. Default: false.
        /// </summary>
        public bool EnablePoolMonitoring { get; set; }

        /// <summary>
        /// Interceptors that run around every query: in order before, in reverse order after or on error.
        /// </summary>
        public List<IQueryInterceptor> Interceptors { get; set; } = [];

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
