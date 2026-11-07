using Microsoft.Data.SqlClient;

namespace SQLBridge {
    /// <summary>
    /// Intercepts query execution at the command level. Implementations can inspect,
    /// modify, or replace commands before execution and process results after.
    /// Multiple interceptors run in registration order (before) and reverse order (after/error).
    /// </summary>
    public interface IQueryInterceptor {

        /// <summary>
        /// Called before a command is executed. Can inspect or modify the command.
        /// Return a non-null InterceptResult to short-circuit execution (skip the database entirely).
        /// </summary>
        ValueTask<InterceptResult?> BeforeExecuteAsync(CommandContext context, CancellationToken ct = default);

        /// <summary>
        /// Called after successful execution. Can inspect or replace the result.
        /// </summary>
        ValueTask AfterExecuteAsync(CommandContext context, CancellationToken ct = default);

        /// <summary>
        /// Called when execution throws an exception. Can suppress the error by setting context.SuppressError = true
        /// and providing a fallback result, or re-throw / wrap the exception.
        /// </summary>
        ValueTask OnErrorAsync(CommandContext context, Exception exception, CancellationToken ct = default);

    }
}
