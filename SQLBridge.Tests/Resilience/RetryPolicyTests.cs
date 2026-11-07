using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace SQLBridge.Tests {
    public class RetryPolicyTests {

        /// <summary>An operation that fails a set number of times and then succeeds.</summary>
        private sealed class FlakyOperation {

            private readonly int _failuresBeforeSuccess;
            private readonly Exception _failure;

            public FlakyOperation(int failuresBeforeSuccess, Exception failure) {
                _failuresBeforeSuccess = failuresBeforeSuccess;
                _failure = failure;
            }

            public int Attempts { get; private set; }

            public Task<string> RunAsync(CancellationToken token) {
                Attempts++;
                if (Attempts <= _failuresBeforeSuccess) {
                    throw _failure;
                }

                return Task.FromResult("ok");
            }

        }

        private static RetryPolicy CreatePolicy(int maxRetries) {
            var options = Options.Create(new SqlBridgeOptions {
                Retry = new RetryOptions {
                    MaxRetryCount = maxRetries,
                    InitialDelay = TimeSpan.FromMilliseconds(1),
                    MaxRetryDelay = TimeSpan.FromMilliseconds(5)
                }
            });
            return new RetryPolicy(options, NullLogger<RetryPolicy>.Instance);
        }

        [Fact]
        public async Task ExecuteAsync_RetriesTransientFailuresThenSucceeds() {
            var operation = new FlakyOperation(failuresBeforeSuccess: 2, new TimeoutException());
            var policy = CreatePolicy(maxRetries: 3);

            var result = await policy.ExecuteAsync<string>(operation.RunAsync);

            Assert.Equal("ok", result);
            Assert.Equal(3, operation.Attempts);
        }

        [Fact]
        public async Task ExecuteAsync_GivesUpAfterMaxRetries() {
            var operation = new FlakyOperation(failuresBeforeSuccess: int.MaxValue, new TimeoutException());
            var policy = CreatePolicy(maxRetries: 2);
            TimeoutException? caught = null;

            try {
                await policy.ExecuteAsync<string>(operation.RunAsync);
            } catch (TimeoutException ex) {
                caught = ex;
            }

            Assert.NotNull(caught);
            Assert.Equal(3, operation.Attempts);
        }

        [Fact]
        public async Task ExecuteAsync_DoesNotRetryNonTransientErrors() {
            var operation = new FlakyOperation(failuresBeforeSuccess: int.MaxValue, new InvalidOperationException());
            var policy = CreatePolicy(maxRetries: 5);
            InvalidOperationException? caught = null;

            try {
                await policy.ExecuteAsync<string>(operation.RunAsync);
            } catch (InvalidOperationException ex) {
                caught = ex;
            }

            Assert.NotNull(caught);
            Assert.Equal(1, operation.Attempts);
        }

        [Fact]
        public void TransientErrorDetector_TreatsTimeoutAsTransient() {
            Assert.True(SqlTransientErrorDetector.IsTransient(new TimeoutException()));
            Assert.False(SqlTransientErrorDetector.IsTransient(new ArgumentException()));
        }

    }
}
