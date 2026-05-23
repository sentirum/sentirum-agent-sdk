# Sentirum.Agent.Providers.Ollama

Ollama provider for the **Sentirum Agent SDK**, backed by
[OllamaSharp](https://www.nuget.org/packages/OllamaSharp).

```csharp
services.AddSentirumAgent("local", b => b
    .UseOllama("llama3.2")                                     // default: http://localhost:11434
    .WithInstructions("You are a local assistant."));

services.AddSentirumAgent("remote", b => b
    .UseOllama(
        model:    "qwen3:32b",
        endpoint: new Uri("http://gpu-host:11434")));
```

## License

MIT
