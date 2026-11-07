# Configuration

All settings live on `SqlBridgeOptions`. Set them in code or bind them from configuration.

## Options

| Option | Default | Description |
| --- | --- | --- |
| `ConnectionString` | none (required) | SQL Server connection string |
| `DefaultCommandTimeout` | `30` | Command timeout in seconds |
| `Retry.MaxRetryCount` | `5` | Retries for transient errors; `0` disables retry |
| `Retry.InitialDelay` | `200 ms` | Wait before the first retry; doubles each attempt, plus up to 50% jitter |
| `Retry.MaxRetryDelay` | `30 s` | Upper limit on one wait |
| `Caching.Enabled` | `true` | When false, `QueryCachedAsync` always hits the database |
| `Caching.DefaultTtl` | `5 min` | Lifetime of a cached result |
| `Caching.MaxEntries` | `1000` | Entries before least-recently-used eviction |
| `Caching.EvictionScanInterval` | `1 min` | How often expired entries are swept |
| `EnablePoolMonitoring` | `false` | Fills `client.Pool` |
| `Interceptors` | empty | Interceptors run around every client query |
| `OnQueryExecuted` | none | Callback with `QueryDiagnostics` after each client query |
| `SlowQueryThreshold` | `1 s` | Queries slower than this are logged as warnings |

## From appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=Insurance;Integrated Security=true;TrustServerCertificate=true"
  },
  "SqlBridge": {
    "DefaultCommandTimeout": 60,
    "EnablePoolMonitoring": true,
    "Retry": { "MaxRetryCount": 3 },
    "Caching": { "DefaultTtl": "00:10:00", "MaxEntries": 5000 }
  }
}
```

```csharp
services.AddSqlBridge(configuration);                 // section "SqlBridge"
services.AddSqlBridge(configuration, "Insurance:Sql"); // another section
```

When the section has no `ConnectionString`, `ConnectionStrings:DefaultConnection` is used, and an
`InvalidOperationException` is thrown if neither is set. `Interceptors` and `OnQueryExecuted` hold code, so
set them in code:

```csharp
services.AddSqlBridge(configuration);
services.Configure<SqlBridgeOptions>(o => o.OnQueryExecuted = d => metrics.Record(d.CommandText, d.Duration));
```

## Logging

`SqlBridge` logs through `ILoggerFactory` when one is available (with dependency injection, that means `AddLogging()` is registered, which ASP.NET does for you; otherwise pass one to
`new SqlBridge(options, loggerFactory)` otherwise). Without one, nothing is logged.

| Level | Event |
| --- | --- |
| Error | a query failed |
| Warning | a query exceeded `SlowQueryThreshold`; a transient error is being retried |
| Debug | query completed; cache hit or miss |
