using Microsoft.Data.SqlClient;

namespace SQLBridge.Model.Interceptors {
    /// <summary>
    /// Returned from BeforeExecuteAsync to short-circuit execution.
    /// The pipeline will skip the database call and use this result instead.
    /// </summary>
    public sealed class InterceptResult {

        /// <summary>The result to return instead of executing the query.</summary>
        public object? Value { get; init; }

        /// <summary>Create a short-circuit result.</summary>
        public static InterceptResult WithValue(object? value) {
            return new InterceptResult {
                Value = value
            };
        }

    }
}
