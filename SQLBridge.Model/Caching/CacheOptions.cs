namespace SQLBridge.Model.Caching {
    public sealed class CacheOptions {
        /// <summary>
        /// Enable or disable the query cache globally. Default: true when cache is registered.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Default TTL for cached query results. Default: 5 minutes.
        /// </summary>
        public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Maximum number of cached entries before LRU eviction kicks in. Default: 1000.
        /// </summary>
        public int MaxEntries { get; set; } = 1000;

        /// <summary>
        /// How often the background timer scans for expired entries. Default: 1 minute.
        /// </summary>
        public TimeSpan EvictionScanInterval { get; set; } = TimeSpan.FromMinutes(1);
    }
}
