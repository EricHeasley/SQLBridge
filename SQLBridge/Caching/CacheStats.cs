namespace SQLBridge {
    /// <summary>
    /// A snapshot of cache activity from <see cref="IQueryCache.GetStats"/>.
    /// </summary>
    /// <param name="EntryCount">Entries currently held.</param>
    /// <param name="HitCount">Lookups served from the cache.</param>
    /// <param name="MissCount">Lookups that went to the database.</param>
    /// <param name="EvictionCount">Entries removed for expiry or capacity.</param>
    public sealed record CacheStats(
        int EntryCount,
        long HitCount,
        long MissCount,
        long EvictionCount) {

        /// <summary>Hits divided by total lookups, from 0 to 1. Zero when there have been no lookups.</summary>
        public double HitRate => HitCount + MissCount == 0 ? 0 : (double)HitCount / (HitCount + MissCount);

    }
}
