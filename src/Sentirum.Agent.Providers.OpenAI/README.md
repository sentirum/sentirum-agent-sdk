# Sentirum.Agent.Providers.OpenAI

OpenAI provider for the **Sentirum Agent SDK**.

```csharp
services.AddSentirumAgent("support", builder => builder
    .UseOpenAI("gpt-4o-mini", apiKey: openAiKey)
    .WithInstructions("You are a helpful Sentirum support agent."));
```

This provider wraps the official [OpenAI .NET SDK](https://www.nuget.org/packages/OpenAI)
through [`Microsoft.Extensions.AI.OpenAI`](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI),
so anything you can layer on top of `IChatClient` (function invocation, OTel,
retries, caching, custom delegating clients) composes naturally.

## License

MIT
