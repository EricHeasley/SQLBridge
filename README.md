# SQLBridge
SQL Client Framework

## Layout

| Project | Contents |
| --- | --- |
| `SQLBridge.Model` | Options, results, stats and context classes (plain data) |
| `SQLBridge.Service.Contracts` | Interfaces (`ISqlClient`, `IQueryCache`, `IQueryInterceptor`, ...) |
| `SQLBridge.Service` | Implementations and DI registration (`AddSqlClient`, ...) |

Register with `services.AddSqlClient(connectionString)` and inject `ISqlClient`.
