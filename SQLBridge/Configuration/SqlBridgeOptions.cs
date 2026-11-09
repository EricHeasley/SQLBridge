namespace SQLBridge {
    /// <summary>
    /// Settings for <see cref="SqlBridgeClient"/>. Bind from configuration or set in code.
    /// </summary>
    public sealed class SqlBridgeOptions {

        /// <summary>SQL Server connection string. Required.</summary>
        public string ConnectionString { get; set; } = string.Empty;
        /// <summary>Command timeout in seconds. Default: 30.</summary>
        public int DefaultCommandTimeout { get; set; } = 30;
        /// <summary>Retry settings for transient errors.</summary>
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
        /// Mappers registered with <see cref="AddMapper{T}"/>, keyed by row type.
        /// </summary>
        internal Dictionary<Type, object> Mappers { get; } = new();

        /// <summary>
        /// Registers a mapper used for <typeparamref name="T"/> in every request, in place of name-based mapping.
        /// A mapper given to <see cref="ISqlRequest.MapWith{T}"/> takes precedence for that request.
        /// </summary>
        public void AddMapper<T>(IResultMapper<T> mapper) where T : new() {
            Mappers[typeof(T)] = mapper;
        }

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
