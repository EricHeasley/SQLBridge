using SQLBridge.Service.Contracts.Interceptors;

namespace SQLBridge.Service.Interceptors {
    /// <summary>
    /// Base class for interceptors. Override only the hooks you need.
    /// </summary>
    public abstract class QueryInterceptorBase : IQueryInterceptor {

        public virtual ValueTask<InterceptResult?> BeforeExecuteAsync(CommandContext context, CancellationToken ct = default) {
            return ValueTask.FromResult<InterceptResult?>(null);
        }

        public virtual ValueTask AfterExecuteAsync(CommandContext context, CancellationToken ct = default) {
            return ValueTask.CompletedTask;
        }

        public virtual ValueTask OnErrorAsync(CommandContext context, Exception exception, CancellationToken ct = default) {
            return ValueTask.CompletedTask;
        }

    }
}
