using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SQLBridge {
    internal sealed class RetryPolicy {

        private readonly RetryOptions _options;
        private readonly ILogger<RetryPolicy> _logger;

        public RetryPolicy(IOptions<SqlBridgeOptions> options, ILogger<RetryPolicy> logger) {
            _options = options.Value.Retry;
            _logger = logger;
        }

        /// <summary>
        /// Runs <paramref name="action"/> with <paramref name="state"/>, retrying transient failures.
        /// The state is passed in so callers can hand over a named method instead of a capturing lambda.
        /// </summary>
        public async Task<T> ExecuteAsync<TState, T>(
            TState state,
            Func<TState, CancellationToken, Task<T>> action,
            CancellationToken ct = default) {
            var attempt = 0;
            while (true) {
                try {
                    return await action(state, ct);
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

        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> action,
            CancellationToken ct = default) {
            return await ExecuteAsync(action, InvokeAsync<T>, ct);
        }

        public async Task ExecuteAsync(
            Func<CancellationToken, Task> action,
            CancellationToken ct = default) {
            await ExecuteAsync(action, InvokeAndDiscardAsync, ct);
        }

        private static async Task<T> InvokeAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token) {
            return await action(token);
        }

        private static async Task<int> InvokeAndDiscardAsync(Func<CancellationToken, Task> action, CancellationToken token) {
            await action(token);
            return 0;
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
