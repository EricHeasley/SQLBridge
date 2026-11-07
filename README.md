# SQLBridge
SQL Client Framework

## Layout

| Project | Contents |
| --- | --- |
| `SQLBridge.Service.Contracts` | Interfaces (`ISqlClient`, `IQueryCache`, ...) plus options, results and other data types |
| `SQLBridge.Service` | Implementations and DI registration (`AddSqlClient`, ...) |

Register with `services.AddSqlClient(connectionString)` and inject `ISqlClient`.
