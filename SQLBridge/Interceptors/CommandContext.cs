namespace SQLBridge {
    /// <summary>
    /// Context object passed through the interceptor pipeline.
    /// Provides access to the command text, parameters, timing, results, and a property bag for passing data between interceptors.
    /// </summary>
    public sealed class CommandContext {

        /// <summary>The original command text (proc name or SQL).</summary>
        public string CommandText { get; init; } = string.Empty;

        /// <summary>The parameters the caller passed (anonymous object, dictionary or SqlParameter array), or null.</summary>
        public object? Parameters { get; init; }

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
