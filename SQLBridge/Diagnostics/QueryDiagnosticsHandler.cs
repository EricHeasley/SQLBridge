using System.Diagnostics;

namespace SQLBridge {
    /// <summary>
    /// Callback for observing query execution. Register via SqlBridgeOptions.
    /// </summary>
    public delegate void QueryDiagnosticsHandler(QueryDiagnostics diagnostics);
}
