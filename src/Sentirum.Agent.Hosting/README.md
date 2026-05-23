# Sentirum.Agent.Hosting

Dependency-injection extensions for the **Sentirum Agent SDK**.

```csharp
services.AddSentirumAgent("support", builder => builder
    .UseOpenAI("gpt-4o-mini")
    .WithInstructions("You are a Sentirum customer support agent."));
```

After registration, resolve agents with `ISentirumAgentRegistry` (all agents)
or by named keyed service.

## License

MIT
