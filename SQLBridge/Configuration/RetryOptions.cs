namespace SQLBridge {
    /// <summary>
    /// Retry behaviour for transient SQL errors: exponential backoff with jitter.
    /// </summary>
    public sealed class RetryOptions {

        /// <summary>How many times a failed call is retried. Default: 5. Use 0 to disable retry.</summary>
        public int MaxRetryCount { get; set; } = 5;

        /// <summary>Upper limit on the wait between attempts. Default: 30 seconds.</summary>
        public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>Wait before the first retry; doubles on each further attempt. Default: 200 ms.</summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    }
}
