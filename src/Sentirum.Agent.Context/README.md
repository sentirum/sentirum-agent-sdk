# Sentirum.Agent.Context

Discoverable builder DX over the MAF `AIContextProvider` pipeline.

```csharp
services.AddSentirumInMemoryMemory();
services.AddSingleton<IKnowledgeBase>(new InMemoryKnowledgeBase(seedSnippets));

services.AddSentirumAgent("support", b => b
    .UseOpenAI("gpt-4o-mini")
    .WithInstructions("You are a Turkish-speaking customer support agent.")
    .WithAmbientInstructions(_ => $"Today is {DateTimeOffset.UtcNow:yyyy-MM-dd}.")
    .WithUserMemory(userId: "u-42")
    .WithKnowledgeBase<IKnowledgeBase>());
```

Three building blocks:

- **`WithAmbientInstructions(...)`** — compute a system message per
  request (date, locale, A/B flag, ...).
- **`WithMemoryContext(partition)` / `WithUserMemory(userId)`** —
  inject every entry from a `Sentirum.Agent.Memory` partition as a
  bulleted block.
- **`WithKnowledgeBase(...)`** — run a search keyed on the latest user
  message and inject the top snippets. Bring your own `IKnowledgeBase`
  or use the bundled `InMemoryKnowledgeBase` for samples.
