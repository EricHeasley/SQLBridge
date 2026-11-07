# SQLBridge
SQL Client Framework

## Layout

Single project, `SQLBridge`, organised by folder: `Core`, `StoredProcedures`, `Transactions`, `BulkOperations`, `Caching`, `Pool`, `Interceptors`, `Mapping`, `Resilience`, `Diagnostics`, `Configuration`.

Register with `services.AddSqlClient(connectionString)` and inject `ISqlClient`.

## Tests

```bash
dotnet test SQLBridge.Tests/SQLBridge.Tests.csproj
```

The tests need no SQL Server. They cover the mapper, cache, retry policy, interceptor pipeline, parameter building, output parameters and DI registration.
