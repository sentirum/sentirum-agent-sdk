# Sentirum.Agent.Memory.Redis

Redis-backed `ISentirumMemoryStore` using `StackExchange.Redis`.

```csharp
services.AddSentirumRedisMemory("localhost:6379");
// or pass an existing IConnectionMultiplexer:
services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(cfg));
services.AddSentirumRedisMemory(opts =>
{
    opts.KeyPrefix = "sentirum:mem:";
    opts.DefaultExpiration = TimeSpan.FromDays(30);
});
```

Storage layout: one Redis hash per partition, fields are entry keys,
absolute expiration uses key-level TTL.
