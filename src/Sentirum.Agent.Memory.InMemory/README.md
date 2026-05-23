# Sentirum.Agent.Memory.InMemory

In-process implementation of `ISentirumMemoryStore`. Suitable for samples,
tests, and single-process apps.

```csharp
services.AddSentirumInMemoryMemory();
```

For distributed scenarios use `Sentirum.Agent.Memory.Redis` or
`Sentirum.Agent.Memory.EntityFrameworkCore` instead.
