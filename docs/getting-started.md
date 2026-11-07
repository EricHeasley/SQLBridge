# Getting started

SQLBridge is a SQL Server client built on `Microsoft.Data.SqlClient`. One class, `SqlBridge`, covers
queries, stored procedures, transactions, bulk copy, caching, retry and diagnostics.

## Install

Reference the `SQLBridge` project (or package). Target framework: .NET 8.

## Create a client

### Without dependency injection

```csharp
using SQLBridge;

using var client = new SqlBridge("Server=.;Database=Insurance;Integrated Security=true;TrustServerCertificate=true");
```

`SqlBridge` is thread-safe and meant to be created once and shared. It implements `IDisposable`
(it owns a cache timer), so dispose it when your application shuts down.

### With dependency injection

```csharp
services.AddSqlBridge("Server=...;Database=...");

// or from configuration (section "SqlBridge", falling back to ConnectionStrings:DefaultConnection)
services.AddSqlBridge(configuration);

// or with full options
services.AddSqlBridge(options => {
    options.ConnectionString = "...";
    options.DefaultCommandTimeout = 60;
});
```

Then take `ISqlClient` in your constructor. It is registered as a singleton, and the container disposes it.

## Define a row type

Columns map to public settable properties by name (case-insensitive). Use `[Column]` when names differ.

```csharp
public class Policy {

    public int PolicyId { get; set; }
    public string? HolderName { get; set; }
    public decimal Premium { get; set; }

    [Column("effective_date")]
    public DateTime EffectiveDate { get; set; }

    public int? ClaimCount { get; set; }

}
```

Row types need a public parameterless constructor. Use nullable types for nullable columns.

## Run commands

```csharp
// Stored procedure: the command text has no whitespace
IReadOnlyList<Policy> policies = await client.QueryAsync<Policy>("usp_GET_Policy", new { Region = "TX" });

// SQL text: the command text contains whitespace
Policy? one = await client.QuerySingleAsync<Policy>("SELECT PolicyId, HolderName, Premium FROM Policy WHERE PolicyId = @PolicyId", new { PolicyId = 1 });

int rows = await client.ExecuteAsync("usp_UPDATE_Policy", new { PolicyId = 1, Status = "Active" });
object? count = await client.ScalarAsync("SELECT COUNT(*) FROM Policy");
```

See [usage](usage.md) for every operation, [configuration](configuration.md) for settings, and
[extending](extending.md) for interceptors and custom mappers.
