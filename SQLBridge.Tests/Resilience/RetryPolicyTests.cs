using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace SQLBridge.Tests {
    public class RetryPolicyTests {

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
            var attempts = 0;
            var policy = CreatePolicy(maxRetries: 3);

            var result = await policy.ExecuteAsync(async token => {
                attempts++;
                if (attempts < 3) {
                    throw new TimeoutException();
                }
                return await Task.FromResult("ok");
            });

            Assert.Equal("ok", result);
            Assert.Equal(3, attempts);
        }

        [Fact]
        public async Task ExecuteAsync_GivesUpAfterMaxRetries() {
            var attempts = 0;
            var policy = CreatePolicy(maxRetries: 2);

            await Assert.ThrowsAsync<TimeoutException>(() => policy.ExecuteAsync<int>(token => {
                attempts++;
                throw new TimeoutException();
            }));

            Assert.Equal(3, attempts);
        }

        [Fact]
        public async Task ExecuteAsync_DoesNotRetryNonTransientErrors() {
            var attempts = 0;
            var policy = CreatePolicy(maxRetries: 5);

            await Assert.ThrowsAsync<InvalidOperationException>(() => policy.ExecuteAsync<int>(token => {
                attempts++;
                throw new InvalidOperationException();
            }));

            Assert.Equal(1, attempts);
        }

        [Fact]
        public void TransientErrorDetector_TreatsTimeoutAsTransient() {
            Assert.True(SqlTransientErrorDetector.IsTransient(new TimeoutException()));
            Assert.False(SqlTransientErrorDetector.IsTransient(new ArgumentException()));
        }

    }
}
