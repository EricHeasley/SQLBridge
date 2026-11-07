namespace SQLBridge {
    public sealed class RetryOptions {
        public int MaxRetryCount { get; set; } = 5;
        public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(200);
    }
}
