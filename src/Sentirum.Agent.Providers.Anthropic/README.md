# Sentirum.Agent.Providers.Anthropic

Anthropic provider for the **Sentirum Agent SDK**. Wraps the official
[Anthropic .NET SDK][anthropic] behind `Microsoft.Extensions.AI.IChatClient`.

```csharp
services.AddSentirumAgent("claude", b => b
    .UseAnthropic("claude-opus-4-6", apiKey: anthropicKey)
    .WithInstructions("You are a Sentirum support agent."));
```

For Anthropic-compatible gateways (Bedrock proxies, Z.AI's Anthropic endpoint,
custom Anthropic-spec gateways) use the
[`Sentirum.Agent.Providers.AnthropicCompatible`](../Sentirum.Agent.Providers.AnthropicCompatible/README.md)
package instead.

## License

MIT

[anthropic]: https://www.nuget.org/packages/Anthropic
