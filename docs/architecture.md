# Architecture

```
SqlBridgeClient (public)               the one class applications use
  └─ Command(...) -> SqlRequest        parameters, mapping and caching as steps, then a terminal call
  └─ SqlClientOperations               connection per call + retry + interceptors + diagnostics
       └─ SqlOperations                shared QueryAsync / ExecuteAsync / ScalarAsync / StreamAsync ...
SqlTransactionWrapper                  same SqlOperations, bound to one connection and transaction
```

`ISqlClient` and `ISqlTransaction` both extend `ISqlOperations`. The only differences between a client call and a
transaction call are where the command comes from (a fresh pooled connection versus the transaction's connection)
and how the run is wrapped (retry, interceptors, logging for the client; none for a transaction).

## Folders

| Folder | Contents |
| --- | --- |
| `Core` | `SqlBridgeClient`, the interfaces `ISqlClient` and `ISqlOperations`, and the shared operations |
| `Requests` | `ISqlRequest` and its implementation, `CommandResult`, `OutputParameterCollection` |
| `Transactions` | `ISqlTransaction` and its implementation |
| `BulkOperations` | bulk copy builder |
| `Caching` | in-memory cache, key builder, `CacheOptions`, `CacheStats` |
| `Pool` | connection monitor and stats |
| `Interceptors` | interceptor interface, base class, pipeline, `CommandContext` |
| `Mapping` | result mappers and `[Column]` |
| `Resilience` | retry policy and transient error detection |
| `Diagnostics` | `QueryDiagnostics`, health check |
| `Configuration` | `SqlBridgeOptions`, `RetryOptions` |

Everything public is in the `SQLBridge` namespace; implementation classes are `internal`.

## Request flow (client)

1. A simple method or a request terminal call builds a `SqlCall`: command text (stored procedure or SQL, decided from the text), parameters, timeout and mappers.
2. For a cached request, the cache is checked first. Otherwise `SqlClientOperations.RunAsync` starts the timer, then runs interceptors if any are registered.
3. The retry policy calls the action, which opens a pooled connection, builds the command, and reads rows.
4. Duration is logged, and `OnQueryExecuted` is invoked with the outcome.

## Tests

`SQLBridge.Tests` covers the mapper, cache, retry policy, interceptors, parameter building, output parameters and
registration without a database. Anything that talks to SQL Server (real execution, transactions, bulk copy)
needs integration tests against a database.
