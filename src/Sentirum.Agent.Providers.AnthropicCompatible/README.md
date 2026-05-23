# Sentirum.Agent.Providers.AnthropicCompatible

Generic Anthropic-compatible provider for the **Sentirum Agent SDK**. Point
the agent at any endpoint that speaks the Anthropic Messages API:

- Z.AI (`https://api.z.ai/api/anthropic`)
- Custom Anthropic-spec gateways and proxies
- Self-hosted Anthropic-compatible servers

```csharp
services.AddSentirumAgent("zai-claude", b => b
    .UseAnthropicCompatible(
        endpoint: new Uri("https://api.z.ai/api/anthropic"),
        model:    "glm-4.7",
        authToken: Environment.GetEnvironmentVariable("ZAI_API_KEY")));
```

`authToken` is sent as a Bearer token (`Authorization: Bearer ...`), which is
what every Anthropic-compatible gateway expects. The native Anthropic API
uses `x-api-key` instead — for the canonical endpoint use
[`Sentirum.Agent.Providers.Anthropic`](../Sentirum.Agent.Providers.Anthropic/README.md).

## License

MIT
