namespace SQLBridge {
    /// <summary>
    /// Timing and outcome of one query, passed to <see cref="SqlBridgeOptions.OnQueryExecuted"/>.
    /// </summary>
    public sealed class QueryDiagnostics {

        /// <summary>The stored procedure name or SQL text that ran.</summary>
        public string CommandText { get; init; } = string.Empty;

        /// <summary>"StoredProcedure" or "Text".</summary>
        public string CommandType { get; init; } = string.Empty;

        /// <summary>Wall-clock time including retries.</summary>
        public TimeSpan Duration { get; init; }

        /// <summary>Rows affected, or -1 when not known (queries that return rows).</summary>
        public int RowsAffected { get; init; }

        /// <summary>True when the query completed without throwing.</summary>
        public bool Succeeded { get; init; }

        /// <summary>The exception message when <see cref="Succeeded"/> is false.</summary>
        public string? ErrorMessage { get; init; }

        /// <summary>Reserved. Retries are logged but not yet counted here.</summary>
        public int RetryCount { get; init; }

    }
}
