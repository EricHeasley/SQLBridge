
namespace SQLBridge.Service.Contracts.Caching {
    public interface IQueryCache {

        /// <summary>
        /// Try to get a cached result. Returns true if found and not expired.
        /// </summary>
        bool TryGet<T>(string key, out IReadOnlyList<T> result);

        /// <summary>
        /// Store a result in the cache with the specified TTL.
        /// </summary>
        void Set<T>(string key, IReadOnlyList<T> result, TimeSpan ttl);

        /// <summary>
        /// Remove a specific cached entry by key.
        /// </summary>
        void Invalidate(string key);

        /// <summary>
        /// Remove all entries whose keys start with the given prefix.
        /// Useful for invalidating all cached results for a specific stored procedure.
        /// </summary>
        void InvalidateByPrefix(string prefix);

        /// <summary>
        /// Clear the entire cache.
        /// </summary>
        void Clear();

        /// <summary>
        /// Get current cache statistics.
        /// </summary>
        CacheStats GetStats();

    }
}
