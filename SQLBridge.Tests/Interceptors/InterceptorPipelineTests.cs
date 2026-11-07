using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using SQLBridge.Interceptors;

namespace SQLBridge.Tests.Interceptors {
    public class InterceptorPipelineTests {

        private sealed class RecordingInterceptor : QueryInterceptorBase {

            private readonly string _name;
            private readonly List<string> _log;

            public RecordingInterceptor(string name, List<string> log) {
                _name = name;
                _log = log;
            }

            public override ValueTask<InterceptResult?> BeforeExecuteAsync(CommandContext context, CancellationToken ct = default) {
                _log.Add($"before:{_name}");
                return base.BeforeExecuteAsync(context, ct);
            }

            public override ValueTask AfterExecuteAsync(CommandContext context, CancellationToken ct = default) {
                _log.Add($"after:{_name}");
                return base.AfterExecuteAsync(context, ct);
            }

        }

        private sealed class ShortCircuitInterceptor : QueryInterceptorBase {

            public override ValueTask<InterceptResult?> BeforeExecuteAsync(CommandContext context, CancellationToken ct = default) {
                return ValueTask.FromResult<InterceptResult?>(InterceptResult.WithValue("cached"));
            }

        }

        private sealed class SuppressingInterceptor : QueryInterceptorBase {

            public override ValueTask OnErrorAsync(CommandContext context, Exception exception, CancellationToken ct = default) {
                context.SuppressError = true;
                context.Result = "fallback";
                return ValueTask.CompletedTask;
            }

        }

        private static InterceptorPipeline CreatePipeline(params IQueryInterceptor[] interceptors) {
            return new InterceptorPipeline(interceptors, NullLogger<InterceptorPipeline>.Instance);
        }

        [Fact]
        public async Task ExecuteAsync_RunsBeforeInOrderAndAfterInReverse() {
            var log = new List<string>();
            var pipeline = CreatePipeline(new RecordingInterceptor("A", log), new RecordingInterceptor("B", log));
            using var command = new SqlCommand();

            await pipeline.ExecuteAsync(command, "usp_GET_Policy", "StoredProcedure",
                (cmd, ct) => Task.FromResult(1), CancellationToken.None);

            Assert.Equal(["before:A", "before:B", "after:B", "after:A"], log);
        }

        [Fact]
        public async Task ExecuteAsync_ShortCircuitSkipsTheDatabase() {
            var pipeline = CreatePipeline(new ShortCircuitInterceptor());
            var executed = false;
            using var command = new SqlCommand();

            var result = await pipeline.ExecuteAsync(command, "usp_GET_Policy", "StoredProcedure", (cmd, ct) => {
                executed = true;
                return Task.FromResult("from database");
            }, CancellationToken.None);

            Assert.Equal("cached", result);
            Assert.False(executed);
        }

        [Fact]
        public async Task ExecuteAsync_InterceptorCanSuppressErrorWithFallback() {
            var pipeline = CreatePipeline(new SuppressingInterceptor());
            using var command = new SqlCommand();

            var result = await pipeline.ExecuteAsync<string>(command, "usp_GET_Policy", "StoredProcedure",
                (cmd, ct) => throw new InvalidOperationException("boom"), CancellationToken.None);

            Assert.Equal("fallback", result);
        }

        [Fact]
        public async Task ExecuteAsync_ErrorPropagatesWhenNotSuppressed() {
            var pipeline = CreatePipeline(new RecordingInterceptor("A", new List<string>()));
            using var command = new SqlCommand();

            await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.ExecuteAsync<string>(
                command, "usp_GET_Policy", "StoredProcedure",
                (cmd, ct) => throw new InvalidOperationException("boom"), CancellationToken.None));
        }

    }
}
