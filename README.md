# SQLBridge
SQL Client Framework

## Layout

Single project, `SQLBridge`, organised by folder: `Core`, `StoredProcedures`, `Transactions`, `BulkOperations`, `Caching`, `Pool`, `Interceptors`, `Mapping`, `Resilience`, `Diagnostics`, `Configuration`.

Register with `services.AddSqlClient(connectionString)` and inject `ISqlClient`.
