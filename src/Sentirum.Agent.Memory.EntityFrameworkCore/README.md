# Sentirum.Agent.Memory.EntityFrameworkCore

EF Core implementation of `ISentirumMemoryStore`. Bring your own
`DbContext` or use the bundled `SentirumMemoryDbContext`.

```csharp
// Quickest path — uses the bundled context against SQLite/SqlServer/etc.
services.AddSentirumEfCoreMemory(opts => opts.UseSqlite("Data Source=sentirum.db"));

// Or plug into your own context:
services.AddDbContext<AppDbContext>(...);
services.AddSentirumEfCoreMemory<AppDbContext>();
```

For shared contexts call
`modelBuilder.ApplySentirumMemoryConfiguration()` inside your
`OnModelCreating` so the unique index over `(Scope, AgentId, UserId,
SessionId, Key)` is created.
