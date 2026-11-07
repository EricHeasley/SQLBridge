using System.Diagnostics;
using SQLBridge.Service.Contracts.Configuration;

namespace SQLBridge.Service.Contracts.Diagnostics {
    /// <summary>
    /// Callback for observing query execution. Register via SqlClientOptions.
    /// </summary>
    public delegate void QueryDiagnosticsHandler(QueryDiagnostics diagnostics);
}
