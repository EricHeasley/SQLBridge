using System.Diagnostics;

namespace SQLBridge {
    /// <summary>
    /// Tracks query execution timing and metadata.
    /// </summary>
    internal sealed class DiagnosticScope : IDisposable {

        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly string _commandText;
        private readonly string _commandType;
        private readonly QueryDiagnosticsHandler? _handler;

        private int _rowsAffected;
        private bool _succeeded;
        private string? _errorMessage;

        public DiagnosticScope(string commandText, string commandType, QueryDiagnosticsHandler? handler) {
            _commandText = commandText;
            _commandType = commandType;
            _handler = handler;
        }

        public void SetResult(int rowsAffected) {
            _rowsAffected = rowsAffected;
            _succeeded = true;
        }

        public void SetSuccess() {
            _succeeded = true;
        }

        public void SetError(Exception ex) {
            _succeeded = false;
            _errorMessage = ex.Message;
        }

        public void Dispose() {
            _stopwatch.Stop();
            _handler?.Invoke(new QueryDiagnostics {
                CommandText = _commandText,
                CommandType = _commandType,
                Duration = _stopwatch.Elapsed,
                RowsAffected = _rowsAffected,
                Succeeded = _succeeded,
                ErrorMessage = _errorMessage
            });
        }

    }
}
