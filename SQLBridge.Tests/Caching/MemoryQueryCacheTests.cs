using SQLBridge.Caching;

namespace SQLBridge.Tests.Caching {
    public class MemoryQueryCacheTests {

        [Fact]
        public void TryGet_ReturnsStoredResult() {
            using var cache = new MemoryQueryCache();
            cache.Set<int>("usp_GET_Policy:abc", [1, 2, 3], TimeSpan.FromMinutes(1));

            var found = cache.TryGet<int>("usp_GET_Policy:abc", out var result);

            Assert.True(found);
            Assert.Equal([1, 2, 3], result);
        }

        [Fact]
        public void TryGet_MissesAfterExpiry() {
            using var cache = new MemoryQueryCache();
            cache.Set<int>("key", [1], TimeSpan.FromMilliseconds(1));
            Thread.Sleep(20);

            Assert.False(cache.TryGet<int>("key", out _));
        }

        [Fact]
        public void InvalidateByPrefix_RemovesOnlyMatchingEntries() {
            using var cache = new MemoryQueryCache();
            cache.Set<int>("usp_GET_Policy:a", [1], TimeSpan.FromMinutes(1));
            cache.Set<int>("usp_GET_Policy:b", [2], TimeSpan.FromMinutes(1));
            cache.Set<int>("usp_GET_Claim:a", [3], TimeSpan.FromMinutes(1));

            cache.InvalidateByPrefix("usp_GET_Policy");

            Assert.False(cache.TryGet<int>("usp_GET_Policy:a", out _));
            Assert.False(cache.TryGet<int>("usp_GET_Policy:b", out _));
            Assert.True(cache.TryGet<int>("usp_GET_Claim:a", out _));
        }

        [Fact]
        public void MaxEntries_EvictsLeastRecentlyUsed() {
            using var cache = new MemoryQueryCache(maxEntries: 2);
            cache.Set<int>("old", [1], TimeSpan.FromMinutes(1));
            Thread.Sleep(5);
            cache.Set<int>("newer", [2], TimeSpan.FromMinutes(1));
            Thread.Sleep(5);
            cache.Set<int>("newest", [3], TimeSpan.FromMinutes(1));

            Assert.False(cache.TryGet<int>("old", out _));
            Assert.True(cache.TryGet<int>("newest", out _));
        }

        [Fact]
        public void GetStats_TracksHitsAndMisses() {
            using var cache = new MemoryQueryCache();
            cache.Set<int>("key", [1], TimeSpan.FromMinutes(1));
            cache.TryGet<int>("key", out _);
            cache.TryGet<int>("missing", out _);

            var stats = cache.GetStats();

            Assert.Equal(1, stats.HitCount);
            Assert.Equal(1, stats.MissCount);
            Assert.Equal(0.5, stats.HitRate);
        }

        [Fact]
        public void CacheKeyBuilder_IsStableRegardlessOfParameterOrder() {
            var first = CacheKeyBuilder.Build("usp_GET_Policy", new { PolicyId = 1, Region = "TX" });
            var second = CacheKeyBuilder.Build("usp_GET_Policy", new { Region = "TX", PolicyId = 1 });
            var different = CacheKeyBuilder.Build("usp_GET_Policy", new { PolicyId = 2, Region = "TX" });

            Assert.Equal(first, second);
            Assert.NotEqual(first, different);
            Assert.StartsWith("usp_GET_Policy:", first);
        }

    }
}
