using System.Diagnostics;
using SQLBridge.Configuration;

namespace SQLBridge.Diagnostics {
    /// <summary>
    /// Callback for observing query execution. Register via SqlClientOptions.
    /// </summary>
    public delegate void QueryDiagnosticsHandler(QueryDiagnostics diagnostics);
}
