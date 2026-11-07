using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace SQLBridge.Tests {
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

        private sealed class ExecutionTracker {

            public bool DatabaseWasCalled { get; set; }

        }

        private static InterceptorPipeline CreatePipeline(params IQueryInterceptor[] interceptors) {
            return new InterceptorPipeline(interceptors, NullLogger<InterceptorPipeline>.Instance);
        }

        private static Task<int> ReturnOneAsync(int state, SqlCommand command, CancellationToken ct) {
            return Task.FromResult(1);
        }

        private static Task<string> ReadFromDatabaseAsync(ExecutionTracker state, SqlCommand command, CancellationToken ct) {
            state.DatabaseWasCalled = true;
            return Task.FromResult("from database");
        }

        private static Task<string> FailAsync(int state, SqlCommand command, CancellationToken ct) {
            throw new InvalidOperationException("boom");
        }

        [Fact]
        public async Task ExecuteAsync_RunsBeforeInOrderAndAfterInReverse() {
            var log = new List<string>();
            var pipeline = CreatePipeline(new RecordingInterceptor("A", log), new RecordingInterceptor("B", log));
            using var command = new SqlCommand();

            await pipeline.ExecuteAsync(0, command, "usp_GET_Policy", "StoredProcedure", ReturnOneAsync, CancellationToken.None);

            Assert.Equal(["before:A", "before:B", "after:B", "after:A"], log);
        }

        [Fact]
        public async Task ExecuteAsync_ShortCircuitSkipsTheDatabase() {
            var pipeline = CreatePipeline(new ShortCircuitInterceptor());
            var tracker = new ExecutionTracker();
            using var command = new SqlCommand();

            var result = await pipeline.ExecuteAsync(tracker, command, "usp_GET_Policy", "StoredProcedure",
                ReadFromDatabaseAsync, CancellationToken.None);

            Assert.Equal("cached", result);
            Assert.False(tracker.DatabaseWasCalled);
        }

        [Fact]
        public async Task ExecuteAsync_InterceptorCanSuppressErrorWithFallback() {
            var pipeline = CreatePipeline(new SuppressingInterceptor());
            using var command = new SqlCommand();

            var result = await pipeline.ExecuteAsync(0, command, "usp_GET_Policy", "StoredProcedure",
                FailAsync, CancellationToken.None);

            Assert.Equal("fallback", result);
        }

        [Fact]
        public async Task ExecuteAsync_ErrorPropagatesWhenNotSuppressed() {
            var pipeline = CreatePipeline(new RecordingInterceptor("A", new List<string>()));
            using var command = new SqlCommand();
            InvalidOperationException? caught = null;

            try {
                await pipeline.ExecuteAsync(0, command, "usp_GET_Policy", "StoredProcedure",
                    FailAsync, CancellationToken.None);
            } catch (InvalidOperationException ex) {
                caught = ex;
            }

            Assert.NotNull(caught);
        }

    }
}
