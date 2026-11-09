# SQLBridge
SQL Client Framework: one class for SQL Server access.

```csharp
using SQLBridge;

// No dependency injection needed
using var client = new SqlBridgeClient("Server=...;Database=...");

// Stored procedure (no whitespace) or SQL (anything with whitespace): same methods
var policies = await client.QueryAsync<Policy>("usp_GET_Policy", new { PolicyId = 1 });
var count = await client.ScalarAsync("SELECT COUNT(*) FROM Policy");

// Parameters, mapping and caching are steps on one request
var cached = await client.Command("usp_GET_Policy")
    .WithParam("Region", "TX")
    .MapWith(new PolicyMapper())
    .CacheFor(TimeSpan.FromMinutes(5))
    .QueryAsync<Policy>();

var result = await client.Command("usp_INSERT_Policy")
    .WithParam("HolderName", "Acme", SqlDbType.NVarChar, 100)
    .WithOutputParam("PolicyId", SqlDbType.Int)
    .ExecuteAsync();

// Transactions and bulk copy

await using var transaction = await client.BeginTransactionAsync();
await transaction.ExecuteAsync("usp_UPDATE_Policy", new { PolicyId = 1, Status = "Active" });
await transaction.CommitAsync();

await client.BulkCopy("Policy").WriteAsync(policies);
```

With dependency injection, one call registers everything:

```csharp
services.AddSqlBridge(options => {
    options.ConnectionString = "...";
    options.EnablePoolMonitoring = true;
    options.Caching.DefaultTtl = TimeSpan.FromMinutes(10);
    options.Interceptors.Add(new MyInterceptor());
});
// or: services.AddSqlBridge(configuration);   // section "SqlBridge" or ConnectionStrings:DefaultConnection
// optional: healthChecks.AddSqlBridgeHealthCheck();
```

`ISqlClient` and `ISqlTransaction` share the same operations (`ISqlOperations`), so code written against a client works unchanged inside a transaction.

## Documentation

| Guide | Covers |
| --- | --- |
| [Getting started](docs/getting-started.md) | creating a client, row types, first queries |
| [Usage](docs/usage.md) | simple methods and requests (parameters, mapping, caching), transactions, bulk copy, health |
| [Configuration](docs/configuration.md) | all options, `appsettings.json`, logging |
| [Extending](docs/extending.md) | interceptors, diagnostics |
| [Architecture](docs/architecture.md) | how the pieces fit |

Every public type and member also has XML documentation for IntelliSense.

## Tests

```bash
dotnet test SQLBridge.Tests/SQLBridge.Tests.csproj
```

The tests need no SQL Server.
