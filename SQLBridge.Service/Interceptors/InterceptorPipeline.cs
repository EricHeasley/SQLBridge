using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SQLBridge.Service.Contracts.Interceptors;

namespace SQLBridge.Service.Interceptors {
    /// <summary>
    /// Executes all registered interceptors in order (before) and reverse order (after/error).
    /// </summary>
    internal sealed class InterceptorPipeline {

        private readonly IQueryInterceptor[] _interceptors;
        private readonly ILogger<InterceptorPipeline> _logger;

        public InterceptorPipeline(
            IEnumerable<IQueryInterceptor> interceptors,
            ILogger<InterceptorPipeline> logger) {
            _interceptors = interceptors.ToArray();
            _logger = logger;
        }

        public bool HasInterceptors => _interceptors.Length > 0;

        /// <summary>
        /// Wraps an async operation with the full interceptor pipeline.
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            SqlCommand command,
            string commandText,
            string commandType,
            Func<SqlCommand, CancellationToken, Task<T>> operation,
            CancellationToken ct) {
            if (_interceptors.Length == 0) {
                return await operation(command, ct);
            }

            var context = new CommandContext {
                Command = command,
                CommandText = commandText,
                CommandType = commandType
            };

            // === Before ===
            for (var i = 0; i < _interceptors.Length; i++) {
                try {
                    var shortCircuit = await _interceptors[i].BeforeExecuteAsync(context, ct);
                    if (shortCircuit is not null) {
                        _logger.LogDebug(
                            "Interceptor {Interceptor} short-circuited {CommandText}",
                            _interceptors[i].GetType().Name, commandText);

                        context.Result = shortCircuit.Value;
                        return (T)shortCircuit.Value!;
                    }
                } catch (Exception ex) {
                    _logger.LogWarning(ex,
                        "Interceptor {Interceptor}.BeforeExecuteAsync threw for {CommandText}",
                        _interceptors[i].GetType().Name, commandText);
                }
            }

            // === Execute ===
            var sw = Stopwatch.StartNew();
            try {
                var result = await operation(context.Command, ct);
                sw.Stop();

                context.Duration = sw.Elapsed;
                context.Result = result;

                // === After (reverse order) ===
                for (var i = _interceptors.Length - 1; i >= 0; i--) {
                    try {
                        await _interceptors[i].AfterExecuteAsync(context, ct);
                    } catch (Exception ex) {
                        _logger.LogWarning(ex,
                            "Interceptor {Interceptor}.AfterExecuteAsync threw for {CommandText}",
                            _interceptors[i].GetType().Name, commandText);
                    }
                }

                // Return potentially modified result
                return context.Result is T modified ? modified : result;
            } catch (Exception ex) {
                sw.Stop();
                context.Duration = sw.Elapsed;

                // === Error (reverse order) ===
                for (var i = _interceptors.Length - 1; i >= 0; i--) {
                    try {
                        await _interceptors[i].OnErrorAsync(context, ex, ct);
                    } catch (Exception innerEx) {
                        _logger.LogWarning(innerEx,
                            "Interceptor {Interceptor}.OnErrorAsync threw for {CommandText}",
                            _interceptors[i].GetType().Name, commandText);
                    }
                }

                if (context.SuppressError && context.Result is T fallback) {
                    _logger.LogDebug(
                        "Error suppressed by interceptor for {CommandText}, using fallback result",
                        commandText);
                    return fallback;
                }

                throw;
            }
        }

    }
}
