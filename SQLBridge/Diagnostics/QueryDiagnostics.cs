using System.Diagnostics;

namespace SQLBridge.Diagnostics {
    public sealed class QueryDiagnostics {
        public string CommandText { get; init; } = string.Empty;
        public string CommandType { get; init; } = string.Empty;
        public TimeSpan Duration { get; init; }
        public int RowsAffected { get; init; }
        public bool Succeeded { get; init; }
        public string? ErrorMessage { get; init; }
        public int RetryCount { get; init; }
    }
}
