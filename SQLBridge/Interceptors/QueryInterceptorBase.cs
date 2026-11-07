
namespace SQLBridge {
    /// <summary>
    /// Base class for interceptors. Override only the hooks you need.
    /// </summary>
    public abstract class QueryInterceptorBase : IQueryInterceptor {

        /// <inheritdoc />
        public virtual ValueTask<InterceptResult?> BeforeExecuteAsync(CommandContext context, CancellationToken ct = default) {
            return ValueTask.FromResult<InterceptResult?>(null);
        }

        /// <inheritdoc />
        public virtual ValueTask AfterExecuteAsync(CommandContext context, CancellationToken ct = default) {
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public virtual ValueTask OnErrorAsync(CommandContext context, Exception exception, CancellationToken ct = default) {
            return ValueTask.CompletedTask;
        }

    }
}
