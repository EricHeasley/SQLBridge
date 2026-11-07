using Microsoft.Data.SqlClient;

namespace SQLBridge.Model.Interceptors {
    /// <summary>
    /// Context object passed through the interceptor pipeline.
    /// Provides access to the command, timing, results, and a property bag for passing data between interceptors.
    /// </summary>
    public sealed class CommandContext {

        /// <summary>The SQL command that will be / was executed.</summary>
        public SqlCommand Command { get; set; } = null!;

        /// <summary>The original command text (proc name or SQL).</summary>
        public string CommandText { get; init; } = string.Empty;

        /// <summary>"StoredProcedure" or "Text".</summary>
        public string CommandType { get; init; } = string.Empty;

        /// <summary>Execution duration (populated after execution).</summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>The raw result object (populated after execution).</summary>
        public object? Result { get; set; }

        /// <summary>Number of rows affected (populated after execution for non-query).</summary>
        public int RowsAffected { get; set; }

        /// <summary>Set to true in OnErrorAsync to suppress the exception and use Result as fallback.</summary>
        public bool SuppressError { get; set; }

        /// <summary>Property bag for passing state between interceptors in the pipeline.</summary>
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();

    }
}
