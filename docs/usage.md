# Usage guide

All examples use `client` (an `ISqlClient`). Every operation below is also available on a transaction,
because `ISqlClient` and `ISqlTransaction` share `ISqlOperations`.

## Stored procedure or SQL?

Each method takes a `commandText`:

| Command text | Treated as |
| --- | --- |
| `usp_GET_Policy`, `dbo.usp_GET_Policy` (no whitespace) | stored procedure |
| `SELECT 1`, `EXEC usp_GET_Policy @Id = 1` (contains whitespace) | SQL text |

A bracketed name with spaces, such as `[my proc]`, is treated as SQL. Use
`client.StoredProcedure("[my proc]")` for that case.

## Parameters

Pass an anonymous object, a POCO, a `Dictionary<string, object?>` or a `SqlParameter[]`. The `@` prefix is
added when missing and `null` becomes `DBNull`.

```csharp
await client.QueryAsync<Policy>("usp_GET_Policy", new { PolicyId = 1, Region = (string?)null });
await client.QueryAsync<Policy>("usp_GET_Policy", new Dictionary<string, object?> { ["PolicyId"] = 1 });
```

## Operations

| Method | Returns | Use for |
| --- | --- | --- |
| `QueryAsync<T>` | `IReadOnlyList<T>` | many rows |
| `QuerySingleAsync<T>` | `T?` | first row or null |
| `ExecuteAsync` | `int` rows affected | INSERT, UPDATE, DELETE |
| `ScalarAsync` | `object?` | one value |
| `StreamAsync<T>` | `IAsyncEnumerable<T>` | large result sets |
| `ExecuteWithOutputAsync<T>` | `StoredProcedureResult<T>` | rows plus output parameters |
| `StoredProcedure(name)` | `IStoredProcedureBuilder` | typed parameters, multiple result sets |
| `BulkCopy(table)` | `IBulkCopyBuilder` | bulk inserts |

### Streaming

```csharp
await foreach (var policy in client.StreamAsync<Policy>("usp_GET_AllPolicies")) {
    Process(policy);
}
```

The connection stays open until the loop ends. Streaming is not retried.

### Output parameters

```csharp
var result = await client.ExecuteWithOutputAsync<Policy>(
    "usp_INSERT_Policy",
    new { HolderName = "Acme" },
    configure: b => b.WithOutputParam("PolicyId", SqlDbType.Int).WithReturnValue());

int id = result.OutputParams.Get<int>("PolicyId");
int? returnValue = result.OutputParams.ReturnValue;
```

### Fluent stored procedure builder

```csharp
var (policies, claims) = await client.StoredProcedure("usp_GET_PolicyWithClaims")
    .WithParam("PolicyId", 1, SqlDbType.Int)
    .WithTimeout(120)
    .QueryMultipleAsync<Policy, Claim>();
```

`QueryMultipleAsync` supports two or three result sets. `WithMapper<T>(mapper)` swaps in a custom mapper.

## Transactions

```csharp
await using var transaction = await client.BeginTransactionAsync();

await transaction.ExecuteAsync("usp_UPDATE_Policy", new { PolicyId = 1, Status = "Active" });
await transaction.BulkCopy("PolicyAudit").WriteAsync(auditRows);

await transaction.CommitAsync();
```

If you dispose the transaction without committing, SQL Server rolls it back. Statements inside a
transaction run once: they are not retried, because retrying part of a transaction is not safe. Interceptors,
caching and diagnostics apply only to calls made on the client.

## Bulk copy

```csharp
await client.BulkCopy("Policy")
    .WithBatchSize(5000)
    .WithTimeout(120)
    .MapColumn("Holder", "HolderName")
    .WriteAsync(policies);       // objects, a DataTable or an IDataReader
```

For objects, public properties of simple types (numbers, strings, dates, `Guid`, `byte[]`, enums) become columns.
Without `MapColumn`, columns match by name.

## Caching

```csharp
var policies = await client.QueryCachedAsync<Policy>("usp_GET_Policy", new { Region = "TX" });
var shortLived = await client.QueryCachedAsync<Policy>("usp_GET_Policy", new { Region = "TX" }, ttl: TimeSpan.FromSeconds(30));

client.InvalidateCache("usp_GET_Policy");   // all parameter combinations of that command
client.Cache.Clear();
var stats = client.Cache.GetStats();        // EntryCount, HitCount, MissCount, EvictionCount, HitRate
```

The cache key is the command text plus parameter values. Only `QueryCachedAsync` reads or writes the cache.
Writes through `ExecuteAsync` do not invalidate it: call `InvalidateCache` after changes. The cache is in
memory, per process, with least-recently-used eviction.

## Health and monitoring

```csharp
await client.PingAsync();   // throws if the database is unreachable

services.AddHealthChecks().AddSqlBridgeHealthCheck();

var stats = client.Pool?.GetStats();   // needs options.EnablePoolMonitoring = true
client.Pool?.ClearPool();              // after a failover or credential rotation
```

`ConnectionPoolStats` reports connections opened, failed and the average open time. ADO.NET does not expose
live pool sizes, so those are not reported.

## Errors and retry

Transient SQL errors (timeouts, dropped connections, Azure SQL "service busy" and similar) are retried with
exponential backoff and jitter. Any other exception is thrown to you unchanged. See
[configuration](configuration.md) for the retry settings.
