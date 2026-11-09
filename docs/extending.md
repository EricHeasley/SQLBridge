# Extending SQLBridge

## Interceptors

An interceptor sees every query run on the client, whether it comes from a simple method or a request. Derive from `QueryInterceptorBase` and override only
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

`CommandContext` carries `CommandText`, `Parameters`, `CommandType`, `Duration`, `Result`, `RowsAffected` and a `Properties`
bag for passing state between interceptors. You never need a `SqlCommand`: interceptors see the text and
parameters of the call, and a short-circuit result replaces the query. Interceptors do not run for
`BulkCopy(...)` or for calls made inside a transaction.

## Custom row mappers

Custom mapping is part of the client: see [mapping in the usage guide](usage.md#mapping). Implement
`IResultMapper<T>`, then use `.MapWith(mapper)` on a request or `options.AddMapper(mapper)` for every request.

## Query diagnostics

```csharp
options.OnQueryExecuted = diagnostics => {
    if (!diagnostics.Succeeded) {
        telemetry.TrackFailure(diagnostics.CommandText, diagnostics.ErrorMessage);
    }
};
```

The callback runs on the calling thread after every client query. Keep it fast and make sure it does not throw.
