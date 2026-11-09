# Usage guide

All examples use `client` (an `ISqlClient`). Every operation below is also available on a transaction,
because `ISqlClient` and `ISqlTransaction` share `ISqlOperations` — except caching, which needs the client.

## Stored procedure or SQL?

Each method takes a `commandText`:

| Command text | Treated as |
| --- | --- |
| `usp_GET_Policy`, `dbo.usp_GET_Policy` (no whitespace) | stored procedure |
| `SELECT 1`, `EXEC usp_GET_Policy @Id = 1` (contains whitespace) | SQL text |

A bracketed name with spaces, such as `[my proc]`, is treated as SQL.

## Two ways to call

**Simple methods** take the command text and a parameters object. Use them when that is all you need.

**A request** (`client.Command(...)`) adds parameters, mapping and caching step by step. Everything the
client can do is a step on the same chain, finished by a terminal call:

```csharp
var policies = await client.Command("usp_GET_Policy")
    .WithParam("Region", "TX")                        // parameters
    .MapWith(new PolicyMapper())                      // mapping
    .CacheFor(TimeSpan.FromMinutes(5))                // caching
    .QueryAsync<Policy>();                            // run
```

## Simple methods

| Method | Returns | Use for |
| --- | --- | --- |
| `QueryAsync<T>` | `IReadOnlyList<T>` | many rows |
| `QuerySingleAsync<T>` | `T?` | first row or null |
| `ExecuteAsync` | `int` rows affected | INSERT, UPDATE, DELETE |
| `ScalarAsync` | `object?` | one value |
| `StreamAsync<T>` | `IAsyncEnumerable<T>` | large result sets |

```csharp
var policies = await client.QueryAsync<Policy>("usp_GET_Policy", new { Region = "TX" });
var one = await client.QuerySingleAsync<Policy>("SELECT PolicyId, HolderName FROM Policy WHERE PolicyId = @PolicyId", new { PolicyId = 1 });
var rows = await client.ExecuteAsync("usp_UPDATE_Policy", new { PolicyId = 1, Status = "Active" });
var count = await client.ScalarAsync("SELECT COUNT(*) FROM Policy");

await foreach (var policy in client.StreamAsync<Policy>("usp_GET_AllPolicies")) {
    Process(policy);
}
```

`parameters` can be an anonymous object, a POCO, a `Dictionary<string, object?>` or a `SqlParameter[]`.
The `@` prefix is added when missing and `null` becomes `DBNull`. Streaming keeps the connection open until the
loop ends and is not retried.

## Requests

### Adding parameters

| Step | Adds |
| --- | --- |
| `WithParam(name, value)` | an input parameter (type inferred from the value) |
| `WithParam(name, value, SqlDbType)` / `(…, size)` | a typed input parameter |
| `WithParams(object)` | several inputs at once: anonymous object, POCO, dictionary or `SqlParameter[]` |
| `WithOutputParam(name, SqlDbType, size)` | an output parameter |
| `WithInputOutput(name, value, SqlDbType, size)` | an input/output parameter |
| `WithReturnValue()` | the procedure's integer return value |
| `WithTimeout(seconds)` | a command timeout for this request |

You can mix them freely. Parameter order does not matter.

### Terminal calls

| Call | Returns |
| --- | --- |
| `QueryAsync<T>()` | `IReadOnlyList<T>` |
| `QuerySingleAsync<T>()` | `T?` |
| `QueryMultipleAsync<T1, T2>()` / `<T1, T2, T3>()` | a tuple of lists, one per result set |
| `ExecuteAsync()` | `CommandResult`: `RowsAffected` and `OutputParams` |
| `ExecuteAsync<T>()` | `CommandResult<T>`: `Data`, `RowsAffected`, `OutputParams` |
| `ScalarAsync()` | `object?` |
| `StreamAsync<T>()` | `IAsyncEnumerable<T>` |

```csharp
var result = await client.Command("usp_INSERT_Policy")
    .WithParam("HolderName", "Acme", SqlDbType.NVarChar, 100)
    .WithOutputParam("PolicyId", SqlDbType.Int)
    .WithReturnValue()
    .ExecuteAsync();

int id = result.OutputParams.Get<int>("PolicyId");
int? returnValue = result.OutputParams.ReturnValue;

var (policies, claims) = await client.Command("usp_GET_PolicyWithClaims")
    .WithParam("PolicyId", 1)
    .QueryMultipleAsync<Policy, Claim>();
```

Retry, interceptors, logging and diagnostics apply to requests exactly as they do to the simple methods.

## Mapping

By default columns map to public settable properties by name (case-insensitive), or by `[Column("name")]`, using
compiled expression trees. For anything else, write an `IResultMapper<T>`:

```csharp
public class PolicyMapper : IResultMapper<Policy> {

    public Policy Map(IDataRecord record) {
        return new Policy {
            PolicyId = record.GetInt32(0),
            HolderName = record.GetString(1)
        };
    }

}
```

Use it for one request, or register it once for every request:

```csharp
client.Command("usp_GET_Policy").MapWith(new PolicyMapper()).QueryAsync<Policy>();   // one request

options.AddMapper(new PolicyMapper());                                               // every request
```

A mapper given to `MapWith` wins over one registered in the options, which wins over name-based mapping.

## Caching

Add `CacheFor` to a `QueryAsync` request:

```csharp
var policies = await client.Command("usp_GET_Policy")
    .WithParam("Region", "TX")
    .CacheFor()                              // uses Caching.DefaultTtl
    .QueryAsync<Policy>();

var shortLived = await client.Command("usp_GET_Policy")
    .WithParam("Region", "TX")
    .CacheFor(TimeSpan.FromSeconds(30))
    .QueryAsync<Policy>();

client.InvalidateCache("usp_GET_Policy");    // every cached result of that command
client.ClearCache();
CacheStats stats = client.GetCacheStats();   // EntryCount, HitCount, MissCount, EvictionCount, HitRate
```

The cache key is the command text, the values of the input parameters and the row type. Output parameters and
parameter order do not matter. Only `QueryAsync` can be cached: `CacheFor` followed by any other terminal call, or
used inside a transaction, throws `InvalidOperationException`. Writes do not clear the cache, so call
`InvalidateCache` after changing data. The cache is in memory, per process, with least-recently-used eviction.
Set `Caching.Enabled = false` to bypass it everywhere.

## Transactions

```csharp
await using var transaction = await client.BeginTransactionAsync();

await transaction.ExecuteAsync("usp_UPDATE_Policy", new { PolicyId = 1, Status = "Active" });
await transaction.Command("usp_INSERT_Audit").WithParam("Action", "Activated").ExecuteAsync();
await transaction.BulkCopy("PolicyAudit").WriteAsync(auditRows);

await transaction.CommitAsync();
```

If you dispose the transaction without committing, SQL Server rolls it back. Statements inside a transaction run
once: they are not retried, because retrying part of a transaction is not safe. Interceptors, caching and
diagnostics apply only to calls made on the client.

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

## Health and monitoring

```csharp
await client.PingAsync();                  // throws if the database is unreachable

services.AddHealthChecks().AddSqlBridgeHealthCheck();

ConnectionPoolStats? pool = client.GetPoolStats();   // null unless options.EnablePoolMonitoring = true
client.ClearPool();                                  // after a failover or credential rotation
```

`ConnectionPoolStats` reports connections opened, failed and the average open time. ADO.NET does not expose
live pool sizes, so those are not reported.

## Errors and retry

Transient SQL errors (timeouts, dropped connections, Azure SQL "service busy" and similar) are retried with
exponential backoff and jitter. Any other exception is thrown to you unchanged. See
[configuration](configuration.md) for the retry settings.
