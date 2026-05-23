# Sentirum.Agent.Providers.OpenAICompatible

Generic OpenAI-compatible provider for the **Sentirum Agent SDK**. Point the
agent at any endpoint that speaks the OpenAI Chat Completions wire format:

- Groq
- Together AI
- vLLM
- LM Studio
- Z.AI (`https://api.z.ai/api/paas/v4`)
- OpenRouter
- Your own self-hosted gateway

```csharp
services.AddSentirumAgent("groq", b => b
    .UseOpenAICompatible(
        endpoint: new Uri("https://api.groq.com/openai/v1"),
        model:    "llama-3.3-70b-versatile",
        apiKey:   Environment.GetEnvironmentVariable("GROQ_API_KEY")));
```

For the most popular endpoints we ship convenience packages (e.g.
`Sentirum.Agent.Providers.ZAI`) that wrap this provider with sensible defaults
and provider-specific extensions.

## License

MIT
