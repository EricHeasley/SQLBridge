namespace SQLBridge.Model.Caching {
    public sealed record CacheStats(
        int EntryCount,
        long HitCount,
        long MissCount,
        long EvictionCount) {
        public double HitRate => HitCount + MissCount == 0 ? 0 : (double)HitCount / (HitCount + MissCount);
    }
}
