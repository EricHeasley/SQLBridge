using System.Collections.Concurrent;
using SQLBridge.Service.Contracts.Caching;

namespace SQLBridge.Service.Caching {
    internal sealed class MemoryQueryCache : IQueryCache, IDisposable {

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly Timer _evictionTimer;
        private readonly int _maxEntries;

        private long _hits;
        private long _misses;
        private long _evictions;

        public MemoryQueryCache(int maxEntries = 1000, TimeSpan? evictionInterval = null) {
            _maxEntries = maxEntries;
            _evictionTimer = new Timer(
                EvictExpired,
                null,
                evictionInterval ?? TimeSpan.FromMinutes(1),
                evictionInterval ?? TimeSpan.FromMinutes(1));
        }

        public bool TryGet<T>(string key, out IReadOnlyList<T> result) {
            if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired) {
                entry.Touch();
                Interlocked.Increment(ref _hits);
                result = (IReadOnlyList<T>)entry.Value;
                return true;
            }

            Interlocked.Increment(ref _misses);

            // Remove if expired
            if (entry is { IsExpired: true }) {
                _cache.TryRemove(key, out _);
                Interlocked.Increment(ref _evictions);
            }

            result = [];
            return false;
        }

        public void Set<T>(string key, IReadOnlyList<T> result, TimeSpan ttl) {
            // Enforce max entries — evict oldest if at capacity
            if (_cache.Count >= _maxEntries && !_cache.ContainsKey(key)) {
                EvictLeastRecentlyUsed();
            }

            _cache[key] = new CacheEntry(result, ttl);
        }

        public void Invalidate(string key) {
            _cache.TryRemove(key, out _);
        }

        public void InvalidateByPrefix(string prefix) {
            foreach (var key in _cache.Keys) {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                    _cache.TryRemove(key, out _);
                }
            }
        }

        public void Clear() {
            _cache.Clear();
        }

        public CacheStats GetStats() {
            return new CacheStats(
                EntryCount: _cache.Count,
                HitCount: Interlocked.Read(ref _hits),
                MissCount: Interlocked.Read(ref _misses),
                EvictionCount: Interlocked.Read(ref _evictions));
        }

        private void EvictExpired(object? state) {
            foreach (var kvp in _cache) {
                if (kvp.Value.IsExpired) {
                    _cache.TryRemove(kvp.Key, out _);
                    Interlocked.Increment(ref _evictions);
                }
            }
        }

        private void EvictLeastRecentlyUsed() {
            // Find the entry with the oldest LastAccessed
            string? oldestKey = null;
            DateTimeOffset oldestTime = DateTimeOffset.MaxValue;

            foreach (var kvp in _cache) {
                if (kvp.Value.LastAccessed < oldestTime) {
                    oldestTime = kvp.Value.LastAccessed;
                    oldestKey = kvp.Key;
                }
            }

            if (oldestKey is not null) {
                _cache.TryRemove(oldestKey, out _);
                Interlocked.Increment(ref _evictions);
            }
        }

        public void Dispose() {
            _evictionTimer.Dispose();
        }

        private sealed class CacheEntry {

            public object Value { get; }
            public DateTimeOffset ExpiresAt { get; }
            public DateTimeOffset LastAccessed { get; private set; }

            public CacheEntry(object value, TimeSpan ttl) {
                Value = value;
                ExpiresAt = DateTimeOffset.UtcNow.Add(ttl);
                LastAccessed = DateTimeOffset.UtcNow;
            }

            public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

            public void Touch() {
                LastAccessed = DateTimeOffset.UtcNow;
            }

        }

    }
}
