# Extending SQLBridge

## Interceptors

An interceptor sees every query run on the client. Derive from `QueryInterceptorBase` and override only
the hooks you need.

| Hook | When | Can |
| --- | --- | --- |
| `BeforeExecuteAsync` | before the query | return `InterceptResult.WithValue(x)` to skip the database and return `x` |
| `AfterExecuteAsync` | after success | read or replace `context.Result` |
| `OnErrorAsync` | after a failure | set `context.SuppressError = true` and `context.Result` to return a fallback |

Before-hooks run in registration order; after and error hooks run in reverse order. An exception thrown inside
an interceptor is logged and ignored, so a faulty interceptor cannot break queries.

```csharp
public class AuditInterceptor : QueryInterceptorBase {

    private readonly IAuditLog _log;

    public AuditInterceptor(IAuditLog log) {
        _log = log;
    }

    public override ValueTask AfterExecuteAsync(CommandContext context, CancellationToken ct = default) {
        _log.Write(context.CommandText, context.Duration);
        return base.AfterExecuteAsync(context, ct);
    }

}
```

Register it:

```csharp
options.Interceptors.Add(new AuditInterceptor(log));       // an instance, in options
services.AddSqlBridgeInterceptor<AuditInterceptor>();       // or built by the container
```

`CommandContext` carries `CommandText`, `CommandType`, `Duration`, `Result`, `RowsAffected` and a `Properties`
bag for passing state between interceptors. Its `Command` is a description of the call with no connection:
changing it does not change the query that runs. Interceptors do not run for calls made through
`StoredProcedure(...)`, `BulkCopy(...)` or inside a transaction.

## Custom row mappers

By default rows map by column name using compiled expression trees (fast, cached per column layout). For
anything else, implement `IResultMapper<T>` and give it to the stored procedure builder:

```csharp
public class PolicyMapper : IResultMapper<Policy> {

    public Policy Map(IDataRecord record) {
        return new Policy {
            PolicyId = record.GetInt32(0),
            HolderName = record.GetString(1)
        };
    }

}

var policies = await client.StoredProcedure("usp_GET_Policy")
    .WithMapper(new PolicyMapper())
    .QueryAsync<Policy>();
```

## Query diagnostics

```csharp
options.OnQueryExecuted = diagnostics => {
    if (!diagnostics.Succeeded) {
        telemetry.TrackFailure(diagnostics.CommandText, diagnostics.ErrorMessage);
    }
};
```

The callback runs on the calling thread after every client query. Keep it fast and make sure it does not throw.
