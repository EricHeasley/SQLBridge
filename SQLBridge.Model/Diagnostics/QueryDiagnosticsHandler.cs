using System.Diagnostics;
using SQLBridge.Model.Configuration;

namespace SQLBridge.Model.Diagnostics {
    /// <summary>
    /// Callback for observing query execution. Register via SqlClientOptions.
    /// </summary>
    public delegate void QueryDiagnosticsHandler(QueryDiagnostics diagnostics);
}
