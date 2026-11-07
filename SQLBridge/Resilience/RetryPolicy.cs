using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SQLBridge.Configuration;

namespace SQLBridge.Resilience {
    internal sealed class RetryPolicy {

        private readonly RetryOptions _options;
        private readonly ILogger<RetryPolicy> _logger;

        public RetryPolicy(IOptions<SqlClientOptions> options, ILogger<RetryPolicy> logger) {
            _options = options.Value.Retry;
            _logger = logger;
        }

        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> action,
            CancellationToken ct = default) {
            var attempt = 0;
            while (true) {
                try {
                    return await action(ct);
                } catch (Exception ex) when (SqlTransientErrorDetector.IsTransient(ex)
                                           && attempt < _options.MaxRetryCount) {
                    attempt++;
                    var delay = CalculateDelay(attempt);

                    _logger.LogWarning(ex,
                        "Transient SQL error on attempt {Attempt}/{MaxRetries}. Retrying after {DelayMs}ms",
                        attempt, _options.MaxRetryCount, delay.TotalMilliseconds);

                    await Task.Delay(delay, ct);
                }
            }
        }

        public async Task ExecuteAsync(
            Func<CancellationToken, Task> action,
            CancellationToken ct = default) {
            await ExecuteAsync(async token => {
                await action(token);
                return 0;
            }, ct);
        }

        private TimeSpan CalculateDelay(int attempt) {
            // Exponential backoff with jitter
            var baseDelay = _options.InitialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
            var jitter = Random.Shared.NextDouble() * baseDelay * 0.5;
            var delay = TimeSpan.FromMilliseconds(baseDelay + jitter);

            return delay > _options.MaxRetryDelay ? _options.MaxRetryDelay : delay;
        }

    }
}
